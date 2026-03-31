# 기찬싱크 (GichanSync) 프로젝트 작업 규칙

## 프로젝트 개요
- 기찬다이어리(WPF) + 기찬뷰어(PWA)의 Firebase Firestore 실시간 동기화
- 기존 프로젝트 소스를 복사하여 독립적으로 Firebase 기능 추가
- 기존 LifeRecord, ExtendPrj 소스코드는 일체 수정하지 않음

## 프로젝트 구조
- src/wpf/ — LifeRecord WPF 복사본 (Firebase 연동 추가)
- src/pwa/ — 기찬뷰어 PWA 복사본 (Firebase 연동 추가)
- src/migration/ — Excel→Firestore 마이그레이션 콘솔 앱
- src/docs/ — 테스트 픽스처 (sample_form.xlsx)

## 기술 스택
- Firebase Firestore (Spark 무료 플랜)
- Firebase Authentication (Google OAuth)
- WPF: Firestore REST API + Google.Apis.Auth
- PWA: Firebase JS SDK v10+ (CDN ES Modules)
- 마이그레이션: .NET 8 콘솔 앱 + ClosedXML

## 상위 프로젝트 참조 (읽기 전용)
- WPF 원본: ../../LifeRecord/src/GichanDiary/
- PWA 원본: ../../ExtendPrj/src/gichan-viewer/
- Excel 데이터: ../../LifeRecord/docs/기찬이_마이그레이션.xlsx

## 핵심 규칙
- ExcelOnly 모드: 기존 기능 100% 하위 호환 (테스트 45개 통과 필수)
- FirebaseSync 모드: Firestore + Excel 이중 쓰기
- 충돌 해결: updatedAt 기반 last-write-wins
- Firestore 리전: asia-northeast1 (도쿄)

## Firebase 프로젝트 정보
- 프로젝트명: gichan-diary (생성 완료)
- 리전: asia-northeast3 (Seoul)
- 인증: Google OAuth (gogokeichi@gmail.com)
- Firestore 스키마: users/{uid}/events/{id}, users/{uid}/settings/app
- firebaseConfig:
  - apiKey: AIzaSyCy6kZ6NK-WltVMvdI5EGLUM7FndWMCjAA
  - authDomain: gichan-diary.firebaseapp.com
  - projectId: gichan-diary
  - storageBucket: gichan-diary.firebasestorage.app
  - messagingSenderId: 1051684985650
  - appId: 1:1051684985650:web:097ea9c25942f7be77952b

## 사용자 역할
- admin: gogokeichi@gmail.com — 전체 권한 + 사용자 관리
- editor: (미지정) — 입력/수정/삭제/설정 (사용자 관리 제외)
- observer: (미지정) — 읽기 전용 (기록/설정 탭 숨김)
- 역할 저장: Firestore /config/roles 문서
- 역할 관리: admin만 가능 (PWA 설정탭 내 관리자 전용 UI)

## Phase 진행 상태
- Phase 1 (기반 설정): ✅ 완료
- Phase 2 (PWA Firebase 연동 + 마이그레이션): ✅ 완료 (449건)
- Phase 3 (WPF Firebase 연동): ✅ 완료 (서비스+ViewModel 전환, 테스트 45개 통과)
- Phase 4 (PWA 전체 기능): ✅ 완료 (입력/수정/삭제/설정 동기화)
- Phase 5 (마무리): Firestore 보안 규칙 교체 필요
- Phase 6 (사용자 역할 관리): 설계 완료, 구현 대기

## Firebase Auth UID
- KrTxuQMTE9Ve2PXJcVTUJmhQntB3 (gogokeichi@gmail.com)
