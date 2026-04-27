# Firestore 보안 규칙 적용 절차

작성일: 2026-04-27
배경: 테스트 모드 30일 만료 임박 (3일 내 모든 클라이언트 요청 차단)
규칙 파일: [`firestore.rules`](./firestore.rules)

---

## 1. 적용 전 준비

### 1-1. 기존 규칙 백업
1. Firebase 콘솔 접속: https://console.firebase.google.com/project/gichan-diary
2. 좌측 메뉴 → **Firestore Database** → 상단 **규칙** 탭
3. 현재 화면의 텍스트 전체 복사 → 메모장이나 docs/firestore_old.rules 등에 임시 보관 (롤백용)

### 1-2. 데이터 구조 사전 확인
- `config/roles` 문서에 다음 필드가 있어야 함:
  - `admin: ["gogokeichi@gmail.com", ...]` (배열)
  - `editor: [...]` (배열)
  - `observer: [...]` (배열)
  - `dataUid: "KrTxuQMTE9Ve2PXJcVTUJmhQntB3"` (문자열)
- 콘솔에서 직접 데이터 보기 → Firestore Database → 데이터 탭 → `config/roles` 확인

---

## 2. 규칙 게시 (실 적용)

1. **Firestore 규칙 탭** 에서 기존 내용 전체 삭제
2. [`firestore.rules`](./firestore.rules) 의 내용 전체 복사 → 붙여넣기
3. 우측 상단 **게시** 버튼 클릭
4. 1~5초 내 반영 (콘솔에 "규칙 게시됨" 토스트)

---

## 3. 즉시 검증 (게시 직후 5분 내)

### 3-1. admin (gogokeichi@gmail.com)
- [ ] PWA 로그인 정상
- [ ] 현황 탭 → 데이터 표시 정상
- [ ] 기록 탭 → 새 이벤트 추가 → 저장 성공
- [ ] 조회 탭 → 기존 이벤트 수정 → 저장 성공
- [ ] 조회 탭 → 이벤트 삭제 → 성공
- [ ] 설정 탭 → 사용자 관리 UI 표시 (역할 추가/삭제 가능)
- [ ] WPF → 내려받기 버튼 → 성공
- [ ] WPF → 올리기 버튼 → 성공

### 3-2. editor (등록된 일반 사용자)
- [ ] PWA 로그인 정상
- [ ] 데이터 조회 정상
- [ ] 이벤트 추가/수정/삭제 → 모두 성공
- [ ] 설정 변경 → 성공
- [ ] **사용자 관리 UI 미표시** (관리자 전용)
- [ ] (개발자도구) 직접 `config/roles` 쓰기 시도 → 거부 확인

### 3-3. observer (등록된 뷰어)
- [ ] PWA 로그인 정상
- [ ] 데이터 조회 정상
- [ ] 기록/설정 탭 미표시
- [ ] (개발자도구) 직접 events 쓰기 시도 → 거부 확인

### 3-4. 비로그인 / 미등록 사용자
- [ ] 비로그인 상태 → 데이터 접근 모두 차단
- [ ] 미등록 이메일로 로그인 → "접근 권한이 없습니다" 안내 표시 (PWA UI 처리)

---

## 4. 모니터링 (게시 후 24시간)

- Firebase 콘솔 → Firestore Database → 사용량 탭
  - 규칙 거부 비율 급증 여부 확인
- Firebase 콘솔 → 인증 → 활성 사용자 추이
- 사용자 피드백 (수동 모니터링)

---

## 5. 비상 롤백

장애 발생 시:
1. Firebase 콘솔 → Firestore → 규칙 탭
2. **백업해 둔 이전 규칙** 붙여넣기 → 게시
3. 즉시(~1초) 반영
4. 원인 분석 후 firestore.rules 수정 → 재시도

만료 전에는 임시로 테스트 모드 복원도 가능:
```javascript
rules_version = '2';
service cloud.firestore {
  match /databases/{database}/documents {
    match /{document=**} {
      allow read, write: if request.time < timestamp.date(2026, 5, 28);
    }
  }
}
```
(만료일을 추가 30일 연장하는 임시 조치 — 어디까지나 비상용)

---

## 6. 적용 후 작업

- [ ] CLAUDE.md의 "Phase 5 (마무리): Firestore 보안 규칙 교체 필요" → "✅ 완료"로 업데이트
- [ ] firestore.rules 파일 git 커밋 (이미 docs/ 추가됨)
- [ ] Firebase 알림 메일 보관 (이력)

---

## 부록: 규칙 동작 원리 요약

```
요청 도착
  ↓
isSignedIn() — 인증 + 이메일 확인
  ↓ (실패: 즉시 차단)
hasRole(요청 경로별 역할)
  ↓ get(config/roles) 1회 추가 read 발생
  ↓ rolesDoc()[role]에 userEmail() 포함 여부 검사
  ↓
허용/거부 결정
```

**비용**: 모든 read 요청마다 `get(config/roles)` 추가 read 1회 발생 → Spark 무료 한도(50K reads/day) 충분히 여유.
