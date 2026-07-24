# AGENTS.md

## 1. 프로젝트 목적

이 프로젝트는 SEM 또는 현미경 이미지에서 밝기값(GV, Gray Value)과 형상 조건을 이용해 미세 입자/점 객체를 검출하고, 각 객체의 크기와 형상 특성을 측정하는 WPF 기반 비전 분석 프로그램이다.

핵심 목표는 다음과 같다.

1. 사용자가 분석 ROI를 직접 지정할 수 있어야 한다.
2. GV, 크기, 형상, 대비 조건을 조합해 객체를 검출할 수 있어야 한다.
3. 검출 결과를 원본 이미지 위에 ROI/윤곽선으로 시각화할 수 있어야 한다.
4. 결과 저장 시 적용된 모든 조건, 원본 이미지, 분석 ROI, 검출 객체 ROI가 함께 기록되어야 한다.
5. 동일한 설정으로 분석을 재현할 수 있어야 한다.
6. 상용 비전 라이브러리 없이 오픈소스 라이브러리만 사용한다.

---

## 2. 기술 스택

- UI: WPF
- Framework: .NET 8 이상
- Language: C#
- Architecture: MVVM
- Image processing: OpenCvSharp
- Image display conversion: OpenCvSharp WPF Extension
- Serialization: `System.Text.Json`
- CSV export: 직접 구현 또는 MIT/Apache-2.0 호환 라이브러리
- Logging: `Microsoft.Extensions.Logging` 또는 Serilog
- Unit test: xUnit 또는 NUnit

권장 NuGet 패키지:

```xml
<PackageReference Include="OpenCvSharp5" />
<PackageReference Include="OpenCvSharp5.runtime.win" />
<PackageReference Include="OpenCvSharp5.WpfExtensions" />
<PackageReference Include="CommunityToolkit.Mvvm" />
```

상용 SDK, 폐쇄형 비전 라이브러리, 라이선스가 불명확한 패키지는 사용하지 않는다.

---

## 3. 기본 개발 원칙

### 3.1 재현성

모든 분석 결과는 다음 정보만으로 동일하게 재현 가능해야 한다.

- 원본 이미지
- 원본 이미지 해시
- 분석 ROI
- 픽셀-길이 보정값
- 전처리 설정
- Threshold 설정
- 활성화된 필터 조건
- 각 필터의 최소값과 최대값
- Morphology 및 Watershed 설정
- 프로그램 버전
- 분석 일시

분석 설정은 JSON으로 저장하고 다시 불러올 수 있어야 한다.

### 3.2 원본 보존

- 원본 이미지는 절대 수정하지 않는다.
- 전처리, 마스크, Overlay 이미지는 별도 객체로 관리한다.
- 분석 중 생성되는 `Mat`은 명확히 복제하거나 소유권을 관리한다.
- `Mat`, `Bitmap`, `Stream` 등 disposable 객체는 반드시 해제한다.

### 3.3 비파괴적 분석

- ROI 변경, 조건 변경, Threshold 변경은 원본 이미지에 영향을 주지 않아야 한다.
- 사용자가 설정을 변경하면 분석 결과만 갱신한다.
- 이전 분석 결과를 Undo/Redo할 수 있도록 설정 스냅샷 구조를 고려한다.

### 3.4 UI 응답성

- 영상 분석은 UI Thread에서 직접 수행하지 않는다.
- `Task`, `CancellationToken`, debounce를 사용한다.
- 슬라이더 이동 중에는 저해상도 Preview 또는 debounce 분석을 사용한다.
- 최종 저장 전에는 반드시 원본 해상도로 재분석한다.

---

## 4. 주요 사용자 흐름

1. 이미지 열기
2. 이미지 확대/축소 및 이동
3. 분석 ROI 지정
4. Scale calibration 수행
5. 전처리 조건 설정
6. GV Threshold 설정
7. 크기/형상/대비 조건 설정
8. 검출 Preview 확인
9. 개별 객체 포함/제외 검토
10. 결과 저장
11. 기존 설정 또는 결과 다시 불러오기

---

## 5. 이미지 입력

지원 형식:

- PNG
- JPEG
- BMP
- TIFF
- 가능하면 8-bit 및 16-bit grayscale 지원

이미지 입력 시 다음 정보를 확인한다.

- 폭과 높이
- 채널 수
- bit depth
- 파일 경로
- 파일명
- 파일 크기
- SHA-256 해시

컬러 이미지가 입력되면 grayscale 변환 방식을 명시적으로 기록한다.

---

## 6. 분석 ROI

### 6.1 ROI 종류

최소 지원:

- Rectangle ROI
- Polygon ROI

추후 확장 가능:

- Circle/Ellipse ROI
- Multiple ROI
- Exclusion ROI

### 6.2 ROI 규칙

- ROI 내부 픽셀만 분석한다.
- ROI 외부는 마스크 처리한다.
- ROI 좌표는 원본 이미지 좌표계로 저장한다.
- 화면 Zoom/Pan 좌표와 원본 이미지 좌표를 혼동하지 않는다.
- ROI에 닿은 객체의 처리 방식을 옵션으로 제공한다.

옵션:

- ROI 경계 접촉 객체 포함
- ROI 경계 접촉 객체 제외
- ROI 경계 접촉 객체 별도 표시

### 6.3 SEM 정보 영역 제외

SEM 이미지 하단의 장비명, 배율, scale bar, 텍스트 영역은 분석 대상에서 제외할 수 있어야 한다.

권장 방식:

- 사용자가 분석 ROI를 직접 지정
- 또는 하단 제외 높이를 설정
- 자동 검출은 선택 기능으로만 제공

---

## 7. Scale calibration

최소 지원 방식:

1. 이미지에서 두 점 선택
2. 실제 길이 입력
3. 단위 선택: nm, µm, mm
4. `length-per-pixel` 계산

저장 항목:

- 시작점 좌표
- 끝점 좌표
- 픽셀 거리
- 실제 길이
- 단위
- µm/pixel 환산값

측정값은 pixel 단위와 실제 길이 단위를 모두 저장한다.

---

## 8. 전처리

각 전처리는 Enable/Disable 가능해야 한다.

### 8.1 기본 전처리

- Grayscale conversion
- Gaussian blur
- Median blur
- Bilateral filter
- CLAHE
- Background subtraction
- Top-hat
- Bottom-hat
- Normalize
- Invert

### 8.2 Morphology

- Erode
- Dilate
- Open
- Close
- Fill holes
- Remove small components

설정 항목:

- Kernel shape
- Kernel size
- Iteration
- 적용 순서

전처리 순서는 분석 설정에 기록한다.

---

## 9. Threshold 및 GV 조건

최소 지원 Threshold 방식:

- Global minimum/maximum GV
- Binary threshold
- Otsu threshold
- Adaptive mean threshold
- Adaptive Gaussian threshold
- InRange

각 객체에 대해 다음 GV 통계를 계산할 수 있어야 한다.

- Minimum GV
- Maximum GV
- Mean GV
- Median GV
- GV standard deviation
- GV percentile
- Object-background GV difference
- Boundary contrast

Threshold 설정과 객체 필터는 구분한다.

예:

- 1차 segmentation: `Min GV <= pixel <= Max GV`
- 2차 object filtering: `Mean GV`, `GV StdDev`, `Local Contrast`

---

## 10. 객체 검출

기본 객체 검출 방식:

- `ConnectedComponentsWithStats`
- 또는 `FindContours`

기본 권장:

- 개수, 면적, bounding box 중심이면 Connected Components
- 둘레, convex hull, 형상 계산이 필요하면 Contour 기반 분석

모든 검출 객체에는 고유 ID를 부여한다.

객체 ID는 결과 저장 시에도 유지되어야 한다.

---

## 11. 객체 측정 항목

### 11.1 위치

- Object ID
- Centroid X/Y
- Bounding box X/Y/Width/Height
- ROI index
- Border contact 여부

### 11.2 크기

- Area, pixel²
- Area, µm²
- Perimeter, pixel
- Perimeter, µm
- Equivalent circular diameter
- Maximum Feret diameter
- Minimum Feret diameter
- Major axis length
- Minor axis length

등가원 직경:

```text
EquivalentDiameter = 2 × sqrt(Area / π)
```

### 11.3 형상

- Aspect ratio
- Circularity
- Solidity
- Convexity
- Extent
- Compactness
- Eccentricity
- Orientation
- Rectangularity
- Elongation
- Roundness
- Hole count
- Euler number

권장 정의:

```text
AspectRatio = MajorAxis / MinorAxis
Circularity = 4 × π × Area / Perimeter²
Solidity = Area / ConvexHullArea
Convexity = ConvexHullPerimeter / ObjectPerimeter
Extent = Area / BoundingBoxArea
Compactness = Perimeter² / (4 × π × Area)
```

정의가 여러 가지인 지표는 프로그램 내부, UI Tooltip, 결과 파일에 사용한 수식을 명시한다.

### 11.4 밝기 및 대비

- Mean GV
- Median GV
- Min GV
- Max GV
- GV standard deviation
- Local background mean GV
- Local contrast
- Boundary gradient
- Boundary contrast

Local background는 객체 마스크를 일정 거리만큼 dilate한 뒤 객체 영역을 제외한 ring 영역으로 계산한다.

설정 항목:

- Ring inner distance
- Ring outer distance
- 최소 유효 배경 픽셀 수

### 11.5 객체 간 관계

- Nearest neighbor distance
- Neighbor count within radius
- Cluster ID
- Local object density
- ROI area fraction

이 항목은 기본 검출 필터보다는 결과 분석용으로 우선 제공한다.

---

## 12. 필터 조건

모든 필터는 다음 구조를 갖는다.

- Enabled
- Minimum
- Maximum
- Unit
- Inclusive/Exclusive
- Invalid value 처리 방식

최소 제공 필터:

- Area
- Equivalent diameter
- Max Feret
- Min Feret
- Major axis
- Minor axis
- Aspect ratio
- Circularity
- Solidity
- Convexity
- Extent
- Eccentricity
- Orientation
- Mean GV
- GV standard deviation
- Local contrast
- Boundary gradient
- Border contact

필터 적용 방식:

```text
FinalAccepted =
    Segmented
    AND all enabled size filters
    AND all enabled shape filters
    AND all enabled GV filters
    AND all enabled contrast filters
    AND border rule
```

어떤 조건에서 객체가 제외되었는지 기록한다.

예:

```json
{
  "objectId": 17,
  "accepted": false,
  "rejectedBy": [
    "Area.Max",
    "Circularity.Min"
  ]
}
```

이 기능은 Threshold 조정과 검출 오류 분석에 필수적이다.

---

## 13. 작은 객체의 신뢰성 처리

매우 작은 객체는 contour 기반 둘레와 형상 지표가 불안정할 수 있다.

권장 정책:

- 면적이 최소 신뢰 픽셀 수 미만인 객체에는 일부 형상값을 `NotReliable`로 표시한다.
- 형상값 계산 실패 시 0을 넣지 말고 `null` 또는 NaN으로 저장한다.
- 사용자가 `Minimum shape measurement area`를 설정할 수 있게 한다.
- 작은 객체에는 GV, area, local contrast 중심으로 필터를 적용할 수 있도록 한다.

---

## 14. 접촉 객체 분리

선택 기능으로 Watershed를 제공한다.

권장 처리:

1. Binary mask 생성
2. Distance transform
3. Marker 생성
4. Marker-based Watershed
5. 분리 객체 재측정

Watershed 적용 조건을 선택할 수 있게 한다.

예:

- Area가 기준보다 큰 객체
- Solidity가 기준보다 낮은 객체
- 단일 객체 내부에 peak가 복수인 객체

모든 객체에 무조건 Watershed를 적용하지 않는다.

저장 항목:

- Watershed 사용 여부
- Seed threshold
- Minimum peak distance
- 적용 전 객체 ID
- 분리 후 객체 ID 목록

---

## 15. 수동 검토

최소 지원:

- 객체 클릭 시 측정값 표시
- 객체 수동 제외
- 제외 객체 다시 포함
- 객체 ID 검색
- 특정 객체 Zoom
- 선택 객체 강조
- accepted/rejected 색상 구분

수동 변경은 자동 조건 결과와 분리해 기록한다.

예:

```json
{
  "automaticAccepted": true,
  "manualOverride": "Exclude",
  "finalAccepted": false,
  "manualNote": "Surface scratch"
}
```

---

## 16. 화면 구성

권장 레이아웃:

```text
┌───────────────────────────────┬────────────────────────────┐
│ 원본/Overlay 이미지 Viewer    │ 조건 설정 패널             │
│ Zoom, Pan, ROI, 객체 선택     │ 전처리, GV, 크기, 형상     │
├───────────────────────────────┼────────────────────────────┤
│ Histogram / Distribution      │ Object DataGrid            │
│ GV, Area, Diameter 등         │ ID, 측정값, 판정, 제외사유 │
└───────────────────────────────┴────────────────────────────┘
```

필수 보기 모드:

- Original
- Preprocessed
- Binary mask
- Accepted objects
- Rejected objects
- Accepted + rejected overlay
- ROI only

Overlay 표시 옵션:

- Contour
- Filled mask
- Bounding box
- Object ID
- Centroid
- Major/minor axis
- Feret line
- Analysis ROI border

---

## 17. 결과 저장 요구사항

결과 저장은 단순 CSV 저장으로 끝내지 않는다.

하나의 분석 결과는 독립된 결과 폴더로 저장한다.

권장 폴더 구조:

```text
Result_YYYYMMDD_HHMMSS/
├─ source/
│  └─ original_image.png
├─ images/
│  ├─ analysis_roi.png
│  ├─ preprocessed.png
│  ├─ binary_mask.png
│  ├─ accepted_mask.png
│  ├─ rejected_mask.png
│  ├─ detection_overlay.png
│  └─ result_summary.png
├─ data/
│  ├─ objects.csv
│  ├─ objects.json
│  ├─ analysis_settings.json
│  └─ run_metadata.json
└─ report/
   └─ result_report.html
```

### 17.1 필수 저장 이미지

#### Original image

- 원본 파일 복사 또는 무손실 저장
- 원본 파일 해시 기록

#### Analysis ROI image

다음 내용을 한 이미지에 표시한다.

- 원본 이미지
- 분석 ROI 경계
- 제외 영역
- Scale calibration line
- ROI 번호 또는 이름

#### Detection overlay image

다음 내용을 포함한다.

- 원본 이미지
- 분석 ROI 경계
- 검출 객체 contour 또는 mask
- 객체 ID
- accepted/rejected 구분
- 잘린 객체 또는 경계 객체 구분
- Scale bar 또는 보정값

#### Result summary image

최종 결과를 한 장으로 확인할 수 있는 합성 이미지다.

반드시 다음 영역을 포함한다.

1. 원본 이미지
2. 분석 ROI가 표시된 이미지
3. 검출 객체가 표시된 Overlay 이미지
4. 적용된 조건 요약
5. 검출 개수 및 주요 통계
6. 파일명, 분석 일시, scale 값

권장 구성:

```text
┌──────────────────────────┬──────────────────────────┐
│ Original + Analysis ROI  │ Detection Overlay        │
├──────────────────────────┼──────────────────────────┤
│ Applied Conditions       │ Summary Statistics       │
└──────────────────────────┴──────────────────────────┘
```

적용 조건 요약에는 활성화된 조건만 표시한다.

예:

```text
Threshold
- GV: 135–255
- Gaussian blur: 3×3

Size
- Area: 5–150 px²
- Equivalent diameter: 0.8–6.0 µm

Shape
- Aspect ratio: 1.0–2.5
- Circularity: 0.30–1.00
- Solidity: 0.70–1.00

Contrast
- Local contrast: ≥ 15 GV

Boundary
- Exclude ROI-border objects: Yes
```

### 17.2 CSV

`objects.csv`에는 최소한 다음 열을 포함한다.

```text
ObjectId
Accepted
AutomaticAccepted
ManualOverride
RejectedBy
CentroidX_px
CentroidY_px
Area_px2
Area_um2
Perimeter_px
EquivalentDiameter_um
MaxFeret_um
MinFeret_um
MajorAxis_um
MinorAxis_um
AspectRatio
Circularity
Solidity
Convexity
Extent
Eccentricity
Orientation_deg
MeanGV
MedianGV
StdDevGV
LocalBackgroundGV
LocalContrastGV
BoundaryGradient
TouchesBorder
BoundingBoxX
BoundingBoxY
BoundingBoxWidth
BoundingBoxHeight
```

### 17.3 분석 설정 JSON

`analysis_settings.json`에는 다음 정보를 저장한다.

- ROI
- Scale calibration
- 전처리 순서와 설정
- Threshold 설정
- 모든 필터의 Enable/Min/Max
- Morphology 설정
- Watershed 설정
- Overlay 설정
- 수동 override

### 17.4 HTML 결과 보고서

HTML 보고서는 외부 서버 없이 열 수 있는 단일 로컬 보고서 형태로 만든다.

포함 내용:

- 원본 이미지
- ROI 이미지
- Overlay 이미지
- 적용 조건 표
- 결과 통계
- 객체 측정 표
- 주요 분포 그래프
- 프로그램 버전
- 파일 해시

---

## 18. 결과 통계

최소 제공:

- 전체 segmented 객체 수
- 조건 통과 객체 수
- 조건 탈락 객체 수
- 수동 제외 객체 수
- 분석 ROI 면적
- 객체 총 면적
- Area fraction
- Number density
- 평균/중앙값/표준편차
- Min/Max
- Percentile: P10, P25, P50, P75, P90

권장 분포:

- Area
- Equivalent diameter
- Max Feret
- Aspect ratio
- Circularity
- Mean GV
- Local contrast

통계에는 어떤 객체 집합을 사용했는지 명시한다.

예:

- Accepted only
- All segmented
- Accepted excluding border objects

---

## 19. 데이터 모델 예시

```csharp
public sealed class RangeFilter
{
    public bool Enabled { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public string Unit { get; set; } = string.Empty;
}

public sealed class ParticleMeasurement
{
    public int ObjectId { get; set; }

    public bool AutomaticAccepted { get; set; }
    public ManualOverrideType ManualOverride { get; set; }
    public bool FinalAccepted { get; set; }

    public List<string> RejectedBy { get; set; } = [];

    public double AreaPixel2 { get; set; }
    public double? AreaUm2 { get; set; }
    public double PerimeterPixel { get; set; }

    public double? EquivalentDiameterUm { get; set; }
    public double? MaxFeretUm { get; set; }
    public double? MinFeretUm { get; set; }

    public double? Circularity { get; set; }
    public double? Solidity { get; set; }
    public double? Convexity { get; set; }
    public double? AspectRatio { get; set; }

    public double MeanGv { get; set; }
    public double MedianGv { get; set; }
    public double StdDevGv { get; set; }
    public double? LocalContrastGv { get; set; }

    public bool TouchesBorder { get; set; }
}
```

---

## 20. 서비스 분리

권장 서비스:

```text
IImageLoader
ICalibrationService
IRoiService
IPreprocessingService
ISegmentationService
IObjectMeasurementService
IObjectFilterService
IOverlayRenderer
IResultExportService
IReportGenerator
IAnalysisPresetService
```

영상 처리 로직을 ViewModel이나 Code-behind에 직접 작성하지 않는다.

### ViewModel 역할

- 사용자 입력 상태 관리
- Command 제공
- 분석 요청 및 취소
- 결과 표시
- 저장 요청

### Service 역할

- OpenCV 처리
- 측정 계산
- 조건 판정
- 이미지 Rendering
- 파일 저장

---

## 21. 로그 및 오류 처리

기록 항목:

- 이미지 로드
- ROI 변경
- Calibration 변경
- 분석 시작/종료
- 분석 시간
- 객체 수
- 저장 경로
- 오류 및 stack trace

사용자 메시지는 이해 가능한 한국어로 표시한다.

예:

- “선택한 ROI가 이미지 범위를 벗어났습니다.”
- “Scale calibration이 없어 실제 길이 단위 측정값은 저장되지 않습니다.”
- “Local contrast 계산에 필요한 주변 배경 픽셀이 부족합니다.”

---

## 22. 성능 요구사항

목표 기준:

- 2K 이하 이미지: 설정 변경 후 Preview 500 ms 이내 목표
- 최종 원본 해상도 분석: 수 초 이내
- 분석 취소 지원
- 메모리 누수 없이 반복 분석 가능
- 수천 개 객체를 DataGrid에서 표시할 때 UI virtualization 사용

최적화 순서:

1. ROI만 처리
2. 불필요한 Mat 복사 제거
3. Preview 해상도 축소
4. 분석 debounce
5. 병렬화는 결과 일관성 검증 후 적용

---

## 23. 테스트 요구사항

### 23.1 단위 테스트

- Equivalent diameter 계산
- Circularity 계산
- Solidity 계산
- Convexity 계산
- Extent 계산
- Scale conversion
- Border contact 판정
- Range filter 판정
- RejectedBy 기록
- JSON 저장/불러오기

### 23.2 합성 이미지 테스트

다음 합성 이미지를 생성해 검증한다.

- 단일 원
- 겹친 원
- 길쭉한 타원
- 오목한 객체
- ROI 경계에 닿은 객체
- 밝기 편차가 있는 객체
- 노이즈가 포함된 객체

### 23.3 회귀 테스트

고정된 테스트 이미지와 설정으로 다음 결과가 일정해야 한다.

- 객체 수
- 객체별 면적
- 평균 직경
- 통과/탈락 객체 ID
- Overlay 이미지

부동소수점 비교에는 허용 오차를 사용한다.

---

## 24. 완료 기준

다음 조건을 모두 만족해야 1차 버전 완료로 본다.

- 이미지 로드 가능
- ROI 지정 가능
- Scale calibration 가능
- GV Threshold 가능
- 크기 및 형상 조건 활성화/비활성화 가능
- 객체별 측정값 계산 가능
- 검출 결과 Overlay 표시 가능
- accepted/rejected 구분 가능
- 객체 클릭 및 수동 제외 가능
- CSV/JSON 저장 가능
- 원본, ROI, Overlay 이미지 저장 가능
- 적용 조건이 포함된 `result_summary.png` 저장 가능
- 설정 JSON을 다시 불러와 동일 분석 재현 가능
- 결과 HTML 보고서 생성 가능

---

## 25. 구현 우선순위

### Phase 1: 핵심 분석

- 이미지 로드
- Rectangle ROI
- Scale calibration
- Grayscale/Gaussian
- GV threshold
- Connected components
- Area, equivalent diameter, perimeter
- Overlay
- CSV/JSON 저장

### Phase 2: 형상 조건

- Feret
- Major/minor axis
- Circularity
- Solidity
- Convexity
- Aspect ratio
- Border filtering
- RejectedBy 기록

### Phase 3: 대비 및 분리

- Local contrast
- Boundary gradient
- Watershed
- Polygon ROI
- Manual override

### Phase 4: 보고서 및 검증

- Result summary image
- HTML report
- Histogram
- Preset
- Regression test
- 성능 최적화

---

## 26. 에이전트 작업 규칙

코드를 수정하는 에이전트는 다음을 준수한다.

1. 구현 전 관련 ViewModel, Service, Model 구조를 먼저 확인한다.
2. 영상 처리 로직을 Code-behind에 추가하지 않는다.
3. 새로운 측정 지표를 추가할 때 정의, 단위, 유효 범위를 함께 문서화한다.
4. 새로운 필터를 추가할 때 저장 JSON, UI, 판정 로직, 결과 보고서를 모두 갱신한다.
5. 결과 파일 형식을 변경할 때 하위 호환성을 검토한다.
6. 숫자 단위 변환을 UI 계층에서 직접 처리하지 않는다.
7. 불확실하거나 계산할 수 없는 값은 0으로 대체하지 않는다.
8. 원본 이미지를 덮어쓰지 않는다.
9. 테스트 없이 핵심 측정 공식을 변경하지 않는다.
10. 결과 이미지와 보고서에는 활성화된 조건만 정확히 표시한다.
11. Preview 결과와 최종 저장 결과가 다른 해상도에서 계산되었는지 명확히 구분한다.
12. 최종 저장 시에는 반드시 원본 해상도로 다시 분석한다.
13. 객체 제외 사유를 추적 가능하게 유지한다.
14. 사용자가 검출 조건을 다시 불러올 수 있도록 모든 분석 파라미터를 직렬화한다.
15. 구현 결과가 요구사항을 만족하지 못하면 추정으로 완료 처리하지 않는다.
