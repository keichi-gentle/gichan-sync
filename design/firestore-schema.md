# Firestore 스키마 설계

## 컬렉션 구조

```
firestore-root/
└── users/
    └── {userId}/                    ← Firebase Auth UID
        ├── events/                  ← BabyEvent 컬렉션
        │   ├── {eventId-1}         ← GUID (WPF BabyEvent.Id와 동일)
        │   ├── {eventId-2}
        │   └── ...
        └── settings/
            └── app                  ← AppSettings 단일 문서
```

## events 문서 필드

| 필드 | 타입 | Excel 컬럼 | 필수 | 설명 |
|------|------|-----------|:---:|------|
| id | string | - | O | GUID (문서 ID와 동일) |
| dayNumber | number | A | | 출생 후 일차 |
| date | timestamp | B | O | 이벤트 날짜 |
| time | string | C | | "HH:mm" 형식 |
| category | string | D | O | 수유/배변/위생관리/신체측정/건강관리/기타 |
| detail | string | E | O | 세부내용 |
| amount | string | F | O | 양 (예: "160ml", "-") |
| note | string | G | | 비고 |
| feedingInterval | string | H | | 수유텀 ("HH:mm") |
| nextExpected | string | I | | 다음 예상 ("HH:mm") |
| dailyFeedTotal | number | J | | 일일 누적 수유량 |
| formulaProduct | string | K | | 분유 제품명 |
| formulaAmount | number | L | | 분유량 (ml) |
| breastfeedAmount | number | M | | 모유수유량 (ml) |
| feedingCount | number | N | | 수유 횟수 (분할) |
| hasUrine | boolean | O | | 소변 여부 |
| hasStool | boolean | P | | 대변 여부 |
| immediateNotice | boolean | Q | | 직후 인지 |
| createdAt | timestamp | - | O | 최초 생성 시각 (서버) |
| updatedAt | timestamp | - | O | 마지막 수정 시각 (서버) |
| source | string | - | O | "wpf" / "pwa" / "migration" |

## settings/app 문서 필드

| 필드 | 타입 | 설명 |
|------|------|------|
| babyName | string | 아기 이름 |
| babyBirthDate | timestamp | 생년월일 |
| formulaProducts | array | 분유 제품 목록 |
| defaultFormulaProduct | string | 기본 분유 제품 |
| fixedFeedingInterval | number | 고정 수유텀 (초) |
| averageFeedingCount | number | 평균 수유텀 계산 횟수 |
| defaultBreastfeedAmount | number | 기본 모유 수유량 |
| defaultFormulaAmount | number | 기본 분유량 |
| theme | string | "Light" / "Dark" |
| updatedAt | timestamp | 마지막 수정 시각 |

## 인덱스

- `events` 컬렉션 기본 인덱스: `date` (DESC) — 자동 생성
- 복합 인덱스 불필요 (단일 사용자, 데이터 소량)

## 용량 예측 (Spark 무료 플랜)

| 항목 | 예상 | 무료 한도 |
|------|------|----------|
| 문서 수 | ~2,000건/년 | 무제한 |
| 저장 용량 | ~5MB/년 | 1GB |
| 일일 읽기 | ~500회 | 50,000회 |
| 일일 쓰기 | ~50회 | 20,000회 |
