# 알려진 잠재 위험 (Known Issues)

실제 운영에서는 발화하지 않거나 발화 가능성이 매우 낮은 잠재 이슈를 기록.
사용자 실제 보고가 들어오면 그때 처리.

---

## 1. `excel-parser.js:78` — 외부 비정규 Excel 임포트 시 UTC 해석 위험

**위치**: `src/pwa/gichan-viewer/js/excel-parser.js` 라인 78

```javascript
function parseDate(val) {
  if (!val) return null;
  if (val instanceof Date && !isNaN(val)) return val;
  const s = String(val).trim();
  const m = s.match(/^(\d{4})[\/\-](\d{1,2})[\/\-](\d{1,2})/);
  if (m) return new Date(+m[1], +m[2] - 1, +m[3]);
  const n = Number(s);
  if (n > 30000 && n < 60000) {
    return new Date((n - 25569) * 86400000);
  }
  const d = new Date(s);   // ← 라인 78 fallback
  return isNaN(d) ? null : d;
}
```

### 발화 조건
- 사용자가 PWA 설정 탭에서 "Excel 파일 가져오기" 실행 시
- AND 임포트 파일이 외부 시스템(다른 양식) 또는 수기 엑셀이라 위 ①~③ 매처에 걸리지 않음
- AND 날짜 셀이 ISO+Z 등 UTC 명시 문자열로 저장된 경우

### 발화 가능성
**낮음** — 우리 시스템(WPF ClosedXML, PWA SheetJS)이 만든 Excel은 항상 ① Date 객체 또는 ② `YYYY-MM-DD`/`YYYY/MM/DD` 정규 포맷으로 저장됨. 외부 파일을 임포트하는 경우에만 발화 가능.

### 영향
- 데이터 손상: 없음 (Date 객체는 절대시각으로 안전 저장됨)
- 표시 불일치: 가능 — KST 자정 직전 데이터가 다른 날짜로 보일 수 있음

### 처리 방침
모니터링. 사용자 실제 보고 들어오면 정확한 포맷 사례에 맞춰 수정.

---

## 2. `firebase-sync.js:83` — Firestore date 필드가 Timestamp 아닌 경우 fallback

**위치**: `src/pwa/gichan-viewer/js/firebase-sync.js` 라인 80-84

```javascript
function parseDate(val) {
  if (!val) return new Date();
  if (val instanceof Date) return val;
  return new Date(val);   // ← 라인 83 fallback
}
```

### 호출 경로
`firebase-sync.js:49` — Firestore에서 events 가져올 때:
```javascript
date: d.date?.toDate ? d.date.toDate() : parseDate(d.date),
```

Timestamp 객체이면 `.toDate()` 호출. 그렇지 않은 경우에만 `parseDate(d.date)` → 위 fallback.

### 발화 조건
Firestore에 `date` 필드가 Timestamp 아닌 형태로 저장된 경우.

우리 시스템은 다음 모두 Timestamp 사용:
- WPF: `FirestoreService.cs:180` `TsVal(...)` (always Timestamp)
- PWA: `event-entry.js:310-313` JS Date 객체 → Firebase SDK가 자동 Timestamp 변환

### 발화 가능성
**거의 0** — 마이그레이션 도구나 외부 스크립트가 잘못된 형식으로 Firestore에 직접 쓴 경우에만.

### 영향
- ISO+Z 문자열을 만나면 UTC 해석되어 `.getDate()`가 KST와 어긋날 가능성

### 처리 방침
방치. 발화 자체가 불가능에 가까움. 외부 마이그레이션 시 형식 검증으로 예방.

---

## 기록 정보

- 발견일: 2026-05-07
- 발견 경로: 16차 피드백 (5/1 일별 수유량 차트 불일치) 분석 중 시간 처리 전수 조사
- 관련 수정: v3.2.4 (`report.js` UTC 버그 3건 수정 완료)
- 본 파일의 2건은 **수정 보류** (사용자 결정)
