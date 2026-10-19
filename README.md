# 🎮 Don't Touch Mine
#### 누구나 숨기고 누구나 찾는, 마지막까지 결과를 알 수 없는 멀티 파티 플레이 게임

<img src="docs/readme_imgs/main.png" width="800" alt="대표 이미지"/>

<br/>

## 📌 목차

- [게임 흐름](#-게임-흐름)
- [주요 기능](#-주요-기능)
  - [게임찾기 & 대기실](#-게임찾기--대기실)
  - [캐릭터 커스텀](#-캐릭터-커스텀)
  - [숨기기](#-숨기기)
  - [플레이](#-플레이)
  - [마지막 30초](#-마지막-30초)
  - [결과 & 하이라이트](#-결과--하이라이트)
- [조작 방법](#-조작-방법)
- [기술 스택](#-기술-스택)
- [팀원](#-팀원)

<br/>

## 🔄 게임 흐름

```
게임찾기 → 대기실 → 숨기기 → 플레이 → 마지막 30초 → 결과 & 하이라이트
```

<br/>

## ✨ 주요 기능

### 🔍 게임찾기 & 대기실

| 게임 찾기 | 방 설정 |
|:---:|:---:|
| <img src="docs/readme_imgs/game_search.png" width="420" alt="게임 찾기"/> | <img src="docs/readme_imgs/방설정.webp" width="420" alt="방 설정"/> |

공개된 방을 고르거나 코드로 들어갈 수 있습니다. 대기실에서 방장은 맵과 경기 규칙을 정합니다.

<br/>

### 🎨 캐릭터 커스텀

| 캐릭터 커스텀 |
|:---:|
| <img src="docs/readme_imgs/character_custom.webp" width="420" alt="캐릭터 커스텀"/> |

몸 색상, 후드, 신발, 표정을 바꿔 나만의 캐릭터를 만듭니다.

<br/>

### 🙈 숨기기

| 숨기기 |
|:---:|
| <img src="docs/readme_imgs/숨기기.webp" width="420" alt="숨기기"/> |

제한 시간 동안 내 물건을 맵 안에 숨깁니다.

<br/>

### 🕹️ 플레이

| 플레이 장면 1 | 플레이 장면 2 |
|:---:|:---:|
| <img src="docs/readme_imgs/플레이장면1.webp" width="420" alt="플레이 장면 1"/> | <img src="docs/readme_imgs/플레이장면2.webp" width="420" alt="플레이 장면 2"/> |

상대의 물건을 찾아 부수고, 공격하여 상대방이 들고 있던 물건을 떨어뜨릴 수 있습니다.


<br/>

### ⏱️ 마지막 30초

| 마지막 30초 |
|:---:|
| <img src="docs/readme_imgs/final_30sec.webp" width="420" alt="마지막 30초"/> |

남은 30초에는 전개가 바뀌고, 끝날 때 내 물건을 들고 있어야 이깁니다.

<br/>

### 🏆 결과 & 하이라이트

| 결과 화면 | 하이라이트 |
|:---:|:---:|
| <img src="docs/readme_imgs/결과화면_Win.webp" width="420" alt="결과 화면"/> | <img src="docs/readme_imgs/하이라이트.webp" width="420" alt="하이라이트"/> |

경기 승패를 확인한 뒤, 그 판에서 남은 장면을 하이라이트로 다시 봅니다.

<br/>

## 👾 조작 방법

| 튜토리얼 |
|:---:|
| <img src="docs/readme_imgs/tutorial.webp" width="420" alt="튜토리얼"/> |

튜토리얼을 통해 기본 키 조작법을 확인할 수 있습니다.


<br/>

## 🛠 기술 스택

| 분야 | 기술 |
|---|---|
| Client | Unity 6 (6000.3), C#, URP |
| Server | Java 21, Spring Boot 4.1, Gradle |
| Realtime | Photon Fusion 2.1, Photon Voice, WebSocket |
| Database | MySQL 8.4, Flyway |
| Infra | AWS EC2, Docker, nginx, Jenkins |

<br/>

## 👥 팀원

| [<img src="https://github.com/jihwan4155.png" width="110" alt="신지환">](https://github.com/jihwan4155) | [<img src="https://github.com/chrin05.png" width="110" alt="김채린">](https://github.com/chrin05) | <img src="https://github.com/hoon4041.png" width="110" alt="심재훈"> | [<img src="https://github.com/hhhhyelim.png" width="110" alt="양혜림">](https://github.com/hhhhyelim) | [<img src="https://github.com/barleymilk.png" width="110" alt="이효주">](https://github.com/barleymilk) | [<img src="https://github.com/hantawon123.png" width="110" alt="한태원">](https://github.com/hantawon123) |
|:---:|:---:|:---:|:---:|:---:|:---:|
| [신지환](https://github.com/jihwan4155) | [김채린](https://github.com/chrin05) | [심재훈](https://github.com/hoon4041) | [양혜림](https://github.com/hhhhyelim) | [이효주](https://github.com/barleymilk) | [한태원](https://github.com/hantawon123) |
| 팀장 / AI | Client / 디자인 | Server | Client / 디자인 | Client / 디자인 | Server |
| 게임 맵 제작<br>AI 봇 개발<br>캐릭터 제작 | 게임 클라이언트<br>캐릭터 디자인<br>게임 UI/UX | 계정 · 친구 서버<br>관리자 페이지<br>금칙어 필터<br>CI/CD | 게임 클라이언트<br>캐릭터 디자인<br>게임 UI/UX | 캐릭터 제작<br>캐릭터 리깅/모션<br>게임 UI/UX | 게임 클라이언트<br>게임 최적화<br>멀티플레이 구현 |
