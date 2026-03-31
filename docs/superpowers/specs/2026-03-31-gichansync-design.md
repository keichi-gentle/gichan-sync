# 기찬싱크 (GichanSync) — PRD 설계 문서

## 1. 개요

| 항목 | 내용 |
|------|------|
| 프로젝트명 | 기찬싱크 (GichanSync) |
| 유형 | Firebase 실시간 동기화 통합 프로젝트 |
| 상위 프로젝트 | LifeRecord (WPF), ExtendPrj (PWA) |
| 목표 | PC↔폰 실시간 데이터 동기화 |
| 인증 | Google OAuth (gogokeichi@gmail.com) |
| 비용 | 0원 (Firebase Spark 무료 플랜) |

## 2. 아키텍처

```
┌─────────────┐              ┌──────────────┐
│  WPF (PC)   │──── REST ───→│   Firebase   │←── onSnapshot ──│  PWA (폰)  │
│ 입력/수정/삭제│              │  Firestore   │                 │ 실시간 조회  │
│ + Excel 백업 │←── 10s poll ─│  (클라우드)   │                 │ (+입력 예정) │
└─────────────┘              └──────────────┘                 └────────────┘
```

## 3. Firestore 스키마

### events 컬렉션
```
users/{userId}/events/{eventId}
  id: string (GUID)
  dayNumber: number | null
  date: timestamp
  time: string | null          ("HH:mm")
  category: string             ("수유"|"배변"|"위생관리"|"신체측정"|"건강관리"|"기타")
  detail: string
  amount: string
  note: string
  feedingInterval: string | null
  nextExpected: string | null
  dailyFeedTotal: number | null
  formulaProduct: string | null
  formulaAmount: number | null
  breastfeedAmount: number | null
  feedingCount: number | null
  hasUrine: boolean | null
  hasStool: boolean | null
  immediateNotice: boolean | null
  createdAt: timestamp
  updatedAt: timestamp
  source: string               ("wpf"|"pwa"|"migration")
```

### settings 문서
```
users/{userId}/settings/app
  babyName: string
  babyBirthDate: timestamp | null
  formulaProducts: array<string>
  defaultFormulaProduct: string
  fixedFeedingInterval: number  (초)
  averageFeedingCount: number
  defaultBreastfeedAmount: number
  defaultFormulaAmount: number
  theme: string
  updatedAt: timestamp
```

## 4. Firestore 보안 규칙

```
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /users/{userId}/{document=**} {
      allow read, write: if request.auth != null && request.auth.uid == userId;
    }
  }
}
```

## 5. WPF 변경사항

### 신규 인터페이스
```csharp
public interface IDataService {
    Task<List<BabyEvent>> LoadEventsAsync();
    Task AddEventAsync(BabyEvent newEvent);
    Task UpdateEventAsync(BabyEvent updated);
    Task DeleteEventAsync(BabyEvent target);
    event Action<List<BabyEvent>>? EventsChanged;
}
```

### 신규 서비스
- `ExcelOnlyDataService` — 기존 IExcelService 래핑 (하위 호환)
- `FirebaseAuthService` — Google OAuth 데스크톱 플로우
- `FirestoreService` — Firestore REST API 클라이언트
- `FirebaseSyncDataService` — 이중 쓰기 (Firestore + Excel)
- `SyncCoordinator` — ExcelOnly / FirebaseSync 모드 전환

### ViewModel 변경
- MainViewModel, EventEntryViewModel, EventListViewModel: IExcelService → IDataService 전환
- SettingsViewModel: 클라우드 동기화 섹션 추가

## 6. PWA 변경사항

### 신규 모듈
- `firebase-config.js` — Firebase 설정값
- `firebase-auth.js` — Google 로그인 (signInWithPopup)
- `firebase-sync.js` — Firestore onSnapshot 실시간 리스너

### 기존 파일 수정
- `app.js` — 인증 상태 관리, Firestore 구독
- `settings.js` — 로그인/로그아웃 UI
- `index.html` — Firebase SDK CDN 로드
- `sw.js` — 캐시 업데이트

## 7. 마이그레이션 도구

- .NET 8 콘솔 앱 (`src/migration/`)
- Excel → Firestore batch write (최대 500건/배치)
- GUID 기반 upsert (중복 실행 안전)

## 8. 사용자 역할 관리

### 역할 정의

| 역할 | 권한 | 탭 구성 |
|------|------|--------|
| **admin** | 전체 (입력/수정/삭제/설정/사용자관리) | 현황/기록/조회/리포트/설정 (5탭) |
| **editor** | 입력/수정/삭제/설정 (사용자관리 제외) | 현황/기록/조회/리포트/설정 (5탭, 사용자관리 숨김) |
| **observer** | 읽기 전용 | 현황/조회/리포트 (3탭, 기록/설정 숨김) |

### Firestore 스키마

```
/config/roles  (단일 문서)
  admin: ['gogokeichi@gmail.com']
  editor: ['편집자@gmail.com']
  observer: ['관찰자@gmail.com']
  updatedAt: timestamp
```

### 보안 규칙

```
// 역할 설정 — 모든 로그인 사용자 읽기, admin만 쓰기
match /config/roles {
  allow read: if request.auth != null;
  allow write: if request.auth.token.email == 'gogokeichi@gmail.com';
}

// 이벤트 데이터 — admin/editor 쓰기, 모든 역할 읽기
match /users/{userId}/events/{eventId} {
  allow read: if request.auth != null;
  allow write: if request.auth.token.email in
    resource.data.admin || request.auth.token.email in resource.data.editor;
}
```

### PWA 동작

1. 로그인 후 `/config/roles` 읽기 → 이메일로 역할 판별
2. 역할에 따라 탭 표시/숨김
3. admin만 설정탭에 "사용자 관리" 섹션 표시
   - 편집자/옵져버 이메일 추가/삭제 UI
   - Firestore `/config/roles` 문서 직접 수정
4. 등록되지 않은 이메일 → 접근 거부 메시지

### 관리자 전용 UI (설정탭 내)

```
┌─────────────────────────────┐
│ 사용자 관리 (관리자 전용)      │
├─────────────────────────────┤
│ 편집자                       │
│ [user1@gmail.com]    [삭제]  │
│ [+ 편집자 추가]              │
├─────────────────────────────┤
│ 옵져버                       │
│ [user2@gmail.com]    [삭제]  │
│ [+ 옵져버 추가]              │
└─────────────────────────────┘
```

---

## 9. Phase 계획

| Phase | 내용 | 세션 수 |
|-------|------|--------|
| 1 | 기반 설정 (폴더/문서/Firebase 프로젝트/마이그레이션) | 1 |
| 2 | PWA Firebase 연동 (인증 + 실시간 리스너) | 1-2 |
| 3 | WPF Firebase 연동 (IDataService + 이중 쓰기) | 2-3 |
| 4 | PWA 입력 기능 (선택) | 1 |
| 5 | 마무리 (충돌 해결, 에러 처리, 문서) | 1 |
| 6 | 사용자 역할 관리 (admin/editor/observer + 관리 UI) | 1 |
