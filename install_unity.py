"""Unity 프로젝트에 AR Foundation / ARCore 패키지와 MetaSonGi AR Recognition 소스를 추가합니다.

사용법:  python install_unity.py "내 Unity 프로젝트 경로" [--dry-run]

- Packages/manifest.json에 패키지가 없으면 추가합니다.
- 이미 다른 버전이 있으면 덮어쓰지 않고 경고만 출력합니다(프로젝트를 깨뜨리지 않기 위해).
- 소스는 Assets/MetaSonGi/ARRecognition/ 아래에 복사합니다. 같은 내용이면 건너뜁니다.
- 여러 번 실행해도 결과가 같습니다.
"""
from __future__ import annotations

import argparse
import filecmp
import json
import shutil
import sys
from pathlib import Path

PACKAGES = {
    "com.unity.xr.arfoundation": "6.3.1",
    "com.unity.xr.arcore": "6.3.1",
}
SOURCES = ["RuntimeImageTracker.cs", "target.png"]
DEST = Path("Assets") / "MetaSonGi" / "ARRecognition"
HERE = Path(__file__).resolve().parent


def is_unity_project(root: Path) -> bool:
    return (root / "Assets").is_dir() and (root / "ProjectSettings").is_dir() and (root / "Packages" / "manifest.json").is_file()


def update_manifest(manifest_path: Path, dry_run: bool = False) -> list[str]:
    """패키지를 추가하고 메시지 목록을 돌려줍니다."""
    data = json.loads(manifest_path.read_text(encoding="utf-8"))
    deps = data.setdefault("dependencies", {})
    msgs, changed = [], False
    for name, ver in PACKAGES.items():
        cur = deps.get(name)
        if cur is None:
            deps[name] = ver
            changed = True
            msgs.append(f"추가: {name} {ver}")
        elif cur == ver:
            msgs.append(f"유지: {name} {ver} (이미 설치됨)")
        else:
            msgs.append(f"경고: {name}이(가) {cur} 버전으로 이미 있습니다. 덮어쓰지 않았습니다 — 필요하면 Package Manager에서 {ver}로 맞추세요.")
    if changed and not dry_run:
        backup = manifest_path.with_suffix(".json.bak")
        if not backup.exists():
            shutil.copy2(manifest_path, backup)
        manifest_path.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return msgs


def copy_sources(root: Path, dry_run: bool = False) -> list[str]:
    dest = root / DEST
    msgs = []
    if not dry_run:
        dest.mkdir(parents=True, exist_ok=True)
    for name in SOURCES:
        src, dst = HERE / name, dest / name
        if not src.exists():
            raise FileNotFoundError(f"저장소에 {name}이(가) 없습니다")
        if dst.exists() and filecmp.cmp(src, dst, shallow=False):
            msgs.append(f"유지: {DEST / name}")
            continue
        if dst.exists():
            msgs.append(f"갱신: {DEST / name} (기존 파일은 .bak으로 보관)")
            if not dry_run:
                shutil.copy2(dst, dst.with_name(dst.name + ".bak"))
        else:
            msgs.append(f"복사: {DEST / name}")
        if not dry_run:
            shutil.copy2(src, dst)
    return msgs


def main(argv: list[str] | None = None) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("project", help="Unity 프로젝트 폴더 (Assets·Packages·ProjectSettings가 있는 곳)")
    ap.add_argument("--dry-run", action="store_true", help="바꾸지 않고 할 일만 출력")
    args = ap.parse_args(argv)

    root = Path(args.project).expanduser().resolve()
    if not is_unity_project(root):
        print(f"오류: Unity 프로젝트가 아닙니다 — {root}\n(Assets, ProjectSettings 폴더와 Packages/manifest.json이 있어야 합니다)", file=sys.stderr)
        return 2
    try:
        msgs = update_manifest(root / "Packages" / "manifest.json", args.dry_run) + copy_sources(root, args.dry_run)
    except (ValueError, OSError) as e:
        print(f"오류: {e}", file=sys.stderr)
        return 1
    print(("[미리보기] " if args.dry_run else "") + f"대상: {root}")
    for m in msgs:
        print(" - " + m)
    print("\n다음: Unity에서 Android로 전환 → XR Plug-in Management > Android > ARCore 체크 → README의 실행 준비 4단계부터 진행")
    return 0


if __name__ == "__main__":
    sys.exit(main())
