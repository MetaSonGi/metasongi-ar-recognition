"""실행: python -m unittest -v   (Unity 없이 설치 스크립트만 검사)"""
import io
import json
import struct
import tempfile
import unittest
import zlib
from contextlib import redirect_stderr, redirect_stdout
from pathlib import Path

import install_unity as iu

HERE = Path(__file__).resolve().parent


def make_project(root: Path, deps=None) -> Path:
    (root / "Assets").mkdir()
    (root / "ProjectSettings").mkdir()
    (root / "Packages").mkdir()
    (root / "Packages" / "manifest.json").write_text(json.dumps({"dependencies": deps or {"com.unity.ugui": "2.0.0"}}), encoding="utf-8")
    return root


def run(*args):
    out, err = io.StringIO(), io.StringIO()
    with redirect_stdout(out), redirect_stderr(err):
        code = iu.main(list(args))
    return code, out.getvalue() + err.getvalue()


class InstallTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)

    def manifest(self):
        return json.loads((self.root / "Packages" / "manifest.json").read_text(encoding="utf-8"))

    def test_rejects_non_unity_folder(self):
        code, out = run(str(self.root))
        self.assertEqual(code, 2)
        self.assertIn("Unity 프로젝트가 아닙니다", out)

    def test_fresh_install_adds_packages_and_sources(self):
        make_project(self.root)
        code, out = run(str(self.root))
        self.assertEqual(code, 0, out)
        deps = self.manifest()["dependencies"]
        self.assertEqual(deps["com.unity.xr.arfoundation"], "6.3.1")
        self.assertEqual(deps["com.unity.xr.arcore"], "6.3.1")
        self.assertEqual(deps["com.unity.ugui"], "2.0.0")          # 기존 의존성 유지
        self.assertTrue((self.root / "Packages" / "manifest.json.bak").exists())
        for name in iu.SOURCES:
            self.assertTrue((self.root / iu.DEST / name).is_file(), name)

    def test_rerun_is_idempotent(self):
        make_project(self.root)
        run(str(self.root))
        before = self.manifest()
        code, out = run(str(self.root))
        self.assertEqual(code, 0)
        self.assertEqual(self.manifest(), before)
        self.assertNotIn("추가:", out)
        self.assertNotIn("복사:", out)

    def test_does_not_overwrite_other_version(self):
        make_project(self.root, {"com.unity.xr.arfoundation": "6.1.0"})
        code, out = run(str(self.root))
        self.assertEqual(code, 0)
        self.assertEqual(self.manifest()["dependencies"]["com.unity.xr.arfoundation"], "6.1.0")
        self.assertIn("경고", out)

    def test_dry_run_changes_nothing(self):
        make_project(self.root)
        before = (self.root / "Packages" / "manifest.json").read_text(encoding="utf-8")
        code, out = run(str(self.root), "--dry-run")
        self.assertEqual(code, 0)
        self.assertIn("[미리보기]", out)
        self.assertEqual((self.root / "Packages" / "manifest.json").read_text(encoding="utf-8"), before)
        self.assertFalse((self.root / iu.DEST).exists())

    def test_modified_source_is_backed_up(self):
        make_project(self.root)
        run(str(self.root))
        dst = self.root / iu.DEST / "RuntimeImageTracker.cs"
        dst.write_text("// 사용자가 수정함", encoding="utf-8")
        code, out = run(str(self.root))
        self.assertIn("갱신", out)
        self.assertEqual((dst.parent / "RuntimeImageTracker.cs.bak").read_text(encoding="utf-8"), "// 사용자가 수정함")


class TargetImageTest(unittest.TestCase):
    """AR 인식용 target.png가 유효한 PNG이고 충분히 크며 무늬가 다양한지 확인."""

    def read_png(self, path):
        raw = path.read_bytes()
        self.assertEqual(raw[:8], b"\x89PNG\r\n\x1a\n")
        pos, idat = 8, b""
        w = h = ctype = depth = None
        while pos < len(raw):
            length, kind = struct.unpack(">I4s", raw[pos:pos + 8])
            data = raw[pos + 8:pos + 8 + length]
            if kind == b"IHDR":
                w, h, depth, ctype = struct.unpack(">IIBB", data[:10])
            elif kind == b"IDAT":
                idat += data
            pos += 12 + length
        return w, h, ctype, depth, zlib.decompress(idat)

    def test_target_png(self):
        w, h, ctype, depth, pixels = self.read_png(HERE / "target.png")
        self.assertGreaterEqual(min(w, h), 512)          # ARCore 권장: 300px 이상
        self.assertIn(ctype, (0, 2, 3))                  # 그레이·RGB·팔레트 (투명도 없음)
        channels = 3 if ctype == 2 else 1
        self.assertEqual(len(pixels), h * (1 + -(-w * channels * depth // 8)))  # 손상 없이 완전히 풀리는지
        # 단색·단순 무늬는 압축이 아주 잘 됨 → 일정 크기 이상이면 특징점이 충분한 복잡한 이미지로 판단
        self.assertGreater((HERE / "target.png").stat().st_size, 15_000)


if __name__ == "__main__":
    unittest.main()
