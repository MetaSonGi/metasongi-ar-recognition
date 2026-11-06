# MetaSonGi AR Recognition

독립된 **2D 이미지 인식 AR 앱**의 Unity/Android 시작 코드입니다. 드론 시뮬레이터·매핑 앱과 별개입니다.

## 구현 방식: 런타임 이미지 등록

이 버전은 SIFT Keypoint/Descriptor를 전달하지 않습니다.

`target.png → Texture2D → MutableRuntimeReferenceImageLibrary → 비동기 등록 검증 → ARTrackedImage → 가상 콘텐츠 위치·회전`

이미지 검출과 자세 추적은 AR Foundation과 기기 ARCore 제공자가 처리합니다. 위치를 외부 CV로 계산하는 SIFT 파이프라인은 별도 연구 범위이며 이 코드에 포함하지 않았습니다.

## 실행 준비

1. Unity 6의 **3D Built-in** 프로젝트를 만듭니다. Android Build Support를 설치하세요.
2. Python 3.10+에서 `python install_unity.py "내 Unity 프로젝트 경로"`를 실행합니다. AR Foundation / ARCore 6.3.1과 소스를 추가합니다. 기존 버전이 다르면 강제 덮어쓰지 않습니다.
3. Unity에서 Android 플랫폼으로 전환하고 **XR Plug-in Management > Android > ARCore**를 활성화합니다. ARCore Project Validation 경고를 해결하세요.
4. 씬에 **AR Session**, **XR Origin (Mobile AR)**를 추가합니다. XR Origin의 카메라에 **AR Camera Manager / AR Camera Background / pose tracking** 구성이 있는지 확인합니다. 일반 Main Camera는 중복되지 않도록 정리합니다.
5. Assets > Create > XR > **Reference Image Library**를 만듭니다. 제공한 `target.png`를 초기 참조 이미지로 추가하고 이름을 `initial-target`, 실제 폭을 **0.20m**로 지정합니다. Texture import의 Read/Write 옵션을 활성화하세요.
6. XR Origin에 **AR Tracked Image Manager**를 추가하고 그 라이브러리를 연결합니다. 같은 오브젝트에 **RuntimeImageTracker**를 붙입니다. Trackable 프리팹은 비워도 기본 큐브가 생성됩니다.
7. **IL2CPP + ARM64**, 카메라 권한, 프로젝트 검증을 확인하고 ARCore 지원 Android 기기에 Build And Run 합니다. `target.png`를 가로 20cm로 출력하거나 화면에 표시하세요.
8. 앱에서 **Register target.png at runtime**을 누릅니다. `Success`를 확인한 뒤 카메라로 참조 이미지를 비춥니다. 초기 이미지 인식과 런타임 등록 테스트를 함께 확인할 수 있습니다.

초기 라이브러리를 사용하는 이유는 XR 이미지 서브시스템을 정상 초기화한 뒤 런타임 추가를 독립적으로 시험하기 위해서입니다. 완전히 빈 라이브러리에서 시작하는 UX와 휴대폰 사진 선택 기능은 후속 범위입니다. 초기 참조 이미지와 런타임 참조 이미지가 같은 패턴이므로 콘텐츠가 중복될 수 있습니다. 순수 런타임 경로 평가 시 초기 라이브러리는 별도 이미지로 교체하세요.

## 동작과 제한

- 런타임 라이브러리 지원 여부와 이미지 등록 성공 상태 검사
- 트래킹이 Limited/None이면 콘텐츠 숨김, 제거된 이미지 콘텐츠 정리
- 원본 이미지·카메라 영상을 외부 서버에 전송하지 않음
- 특정 물체를 분류하는 AI 앱, 공간 스캔, 프로젝터 자동 보정 앱은 아님
- SIFT 특징점을 AR Foundation 이미지 라이브러리에 직접 주입하는 코드는 없음
- Unity 편집기·Android SDK·실제 AR 기기가 이 환경에 없어 **C# 컴파일·APK·카메라 추적은 미검증**입니다. 설치 스크립트는 임시 Unity 프로젝트에서 생성·재실행을 검사했습니다.

## 공식 참고

- [이미지 추적 문서](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/manual/features/image-tracking.html)
- [런타임 이미지 라이브러리 API](https://docs.unity3d.com/Packages/com.unity.xr.arfoundation@6.3/api/UnityEngine.XR.ARSubsystems.MutableRuntimeReferenceImageLibrary.html)

## 다음 단계

- [ ] Android 사진 선택과 파일 검증
- [ ] 여러 이미지별 콘텐츠 설정
- [ ] 등록 이미지 목록과 세션 저장
- [ ] SIFT 직접 매칭 별도 실험·정량 비교
