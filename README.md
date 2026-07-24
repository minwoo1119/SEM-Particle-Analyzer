# SEM Particle Analyzer

SEM 및 현미경 이미지에서 Gray Value와 형상 조건을 이용해 미세 입자를 검출하고 측정하는 Windows 데스크톱 프로그램입니다.

## 개발 환경

- .NET 10 / WPF
- C# / MVVM
- OpenCvSharp
- Windows x64

## 현재 구현 범위

- PNG, JPEG, BMP, TIFF 이미지 로드와 SHA-256 기록
- 원본 좌표계 기반 Rectangle ROI 드래그
- Gaussian blur, CLAHE, 반전, morphology open
- InRange, Binary, Otsu threshold
- Contour 기반 면적, 둘레, 등가원 직경, circularity, solidity, GV 측정
- 필터별 제외 사유와 ROI 경계 접촉 판정
- Original, Preprocessed, Binary mask, Overlay 보기
- CSV, JSON, 분석 이미지, 로컬 HTML 보고서 내보내기
- 분석 설정 JSON 저장 및 불러오기
- 분석 취소와 원본 해상도 최종 재분석

## 실행

```powershell
dotnet restore
dotnet run --project "SEM Particle Analyzer.csproj"
```

이미지를 연 뒤 Viewer에서 드래그하여 ROI를 지정하고 `분석 실행`을 누릅니다.
