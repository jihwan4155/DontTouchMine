<p align="right"><a href="README.md">KR</a> / <a href="README.en.md"><strong>EN</strong></a></p>

# 🎮 Don't Touch Mine
#### A multiplayer party game where everyone hides and everyone searches—and the outcome stays uncertain until the very end

<img src="docs/readme_imgs/main.png" width="800" alt="Cover image"/>

<br/>

- **Development period**: 2026.08.17 ~ 2026.09.28 (SSAFY Cohort 15 Specialization Project)
- **Award 🏆**: Best Project Award (1st place)
- **In development for release on Steam**

## 📌 Table of Contents

- [Game Flow](#-game-flow)
- [Key Features](#-key-features)
  - [Find a Game & Lobby](#-find-a-game--lobby)
  - [Character Customization](#-character-customization)
  - [Dances & Emotes](#-dances--emotes)
  - [Hiding](#-hiding)
  - [Gameplay](#️-gameplay)
  - [Final 30 Seconds](#️-final-30-seconds)
  - [Results & Highlights](#-results--highlights)
- [Controls](#-controls)
- [Tech Stack](#-tech-stack)
- [Team](#-team)

<br/>

## 🔄 Game Flow

```
Find a Game → Lobby → Hiding → Gameplay → Final 30 Seconds → Results & Highlights
```

<br/>

## ✨ Key Features

### 🔍 Find a Game & Lobby

| Find a Game | Room Settings |
|:---:|:---:|
| <img src="docs/readme_imgs/game_search.png" width="420" alt="Find a game"/> | <img src="docs/readme_imgs/방설정.webp" width="420" alt="Room settings"/> |

Choose a public room or join with a code. In the lobby, the host selects the map and sets the match rules.

<br/>

### 🎨 Character Customization

| Character Customization |
|:---:|
| <img src="docs/readme_imgs/character_custom.webp" width="420" alt="Character customization"/> |

Create your own character by changing its body color, hood, shoes, and facial expression.

<br/>

### 💃 Dances & Emotes

Express your character's mood through dances and a variety of other emotes, and have fun with other players.

<br/>

### 🙈 Hiding

| Hiding |
|:---:|
| <img src="docs/readme_imgs/숨기기.webp" width="420" alt="Hiding"/> |

Hide your item somewhere on the map within the time limit.

Items can be freely rotated to any angle before placement. Lay them flat or tilt them to fit the surroundings and create your own hiding spot.

<br/>

### 🕹️ Gameplay

| Gameplay Scene 1 | Gameplay Scene 2 |
|:---:|:---:|
| <img src="docs/readme_imgs/플레이장면1.webp" width="420" alt="Gameplay scene 1"/> | <img src="docs/readme_imgs/플레이장면2.webp" width="420" alt="Gameplay scene 2"/> |

Find and break your opponents' items, or attack opponents to make them drop the items they are carrying.

All assets and objects on the map are interactive. Use nearby objects and spaces to make hiding and searching even more fun.


<br/>

### ⏱️ Final 30 Seconds

| Final 30 Seconds |
|:---:|
| <img src="docs/readme_imgs/final_30sec.webp" width="420" alt="Final 30 seconds"/> |

The game takes a turn in the final 30 seconds: to win, you must be holding your own item when the match ends.

<br/>

### 🏆 Results & Highlights

| Results Screen | Highlights |
|:---:|:---:|
| <img src="docs/readme_imgs/결과화면_Win.webp" width="420" alt="Results screen"/> | <img src="docs/readme_imgs/하이라이트.webp" width="420" alt="Highlights"/> |

Check the match results, then relive memorable moments from the match in the highlights.

<br/>

## 👾 Controls

| Tutorial |
|:---:|
| <img src="docs/readme_imgs/tutorial.webp" width="420" alt="Tutorial"/> |

Learn the basic keyboard controls through the tutorial.


<br/>

## 🛠 Tech Stack

| Area | Technologies |
|---|---|
| Client | Unity 6 (6000.3), C#, URP |
| Server | Java 21, Spring Boot 4.1, Gradle |
| Realtime | Photon Fusion 2.1, Photon Voice, WebSocket |
| Database | MySQL 8.4, Flyway |
| Infra | AWS EC2, Docker, nginx, Jenkins |

<br/>

## 👥 Team

| [<img src="https://github.com/jihwan4155.png" width="110" alt="Jihwan Shin">](https://github.com/jihwan4155) | [<img src="https://github.com/chrin05.png" width="110" alt="Chaerin Kim">](https://github.com/chrin05) | <img src="https://github.com/hoon4041.png" width="110" alt="Jaehoon Shim"> | [<img src="https://github.com/hhhhyelim.png" width="110" alt="Hyelim Yang">](https://github.com/hhhhyelim) | [<img src="https://github.com/barleymilk.png" width="110" alt="Hyoju Lee">](https://github.com/barleymilk) | [<img src="https://github.com/hantawon123.png" width="110" alt="Taewon Han">](https://github.com/hantawon123) |
|:---:|:---:|:---:|:---:|:---:|:---:|
| [Jihwan Shin](https://github.com/jihwan4155) | [Chaerin Kim](https://github.com/chrin05) | [Jaehoon Shim](https://github.com/hoon4041) | [Hyelim Yang](https://github.com/hhhhyelim) | [Hyoju Lee](https://github.com/barleymilk) | [Taewon Han](https://github.com/hantawon123) |
| Team Lead / AI | Client / Design | Server | Client / Design | Client / Design | Server |
| Game map creation<br>AI bot development<br>Character creation | Game client<br>Character design<br>Game UI/UX | Account and friend services<br>Admin page<br>Profanity filter<br>CI/CD | Game client<br>Character design<br>Game UI/UX | Character creation<br>Character rigging/animation<br>Game UI/UX | Game client<br>Game optimization<br>Multiplayer implementation |
