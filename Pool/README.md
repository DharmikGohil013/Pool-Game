# 🎱 3D Pool Game (Unity)

A 3D 8-Ball Pool simulator built in Unity featuring physics-based gameplay, aim prediction lines, and full 2-player turn logic.

---

## 🎮 Gameplay Features

* **Realistic 3D Physics:** Accurate ball collisions, bank shots, and cue stick power charging.
* **Aiming Trajectory Guide:** Visual prediction lines for cue path, target collision, and deflection angle.
* **Dynamic 2-Player Rules:** Automatic assignment of **Solids** vs. **Stripes** on the first pocketed ball.
* **Foul & Re-spotting:** Automatic cue ball reset on fouls (scratching into a pocket or falling off table).
* **Cross-Platform Controls:** Supports Mouse, Touch screen, and Keyboard.

---

## 🕹️ Controls

| Action | Mouse | Touch | Keyboard |
| :--- | :--- | :--- | :--- |
| **Aiming** | Move / Drag Mouse | Drag across screen | `Left` / `Right` Arrow keys or `A` / `D` |
| **Charge Power** | Hold **Left Click** | Hold touch for > 0.25s | Press & Hold `Spacebar` |
| **Shoot** | Release **Left Click** | Release touch | Release `Spacebar` |

---

## 🏆 Game Rules & Flow

1. **Break Shot:** Player 1 starts by breaking the racked balls.
2. **Type Assignment:** The first non-cue ball pocketed assigns that ball type (**Solids** or **Stripes**) to the active player.
3. **Turn Rules:**
   - Pocketing your assigned ball grants an extra shot.
   - Pocketing an opponent's ball or failing to pocket switches the turn.
   - Pocketing the cue ball (**Scratch**) penalty resets cue ball position and switches turn.
4. **Winning:** Sink all your assigned balls first, then pocket the 8-Ball!

---

## 🚀 How to Run

1. Open **Unity Hub** and add this folder as a project.
2. Open the main scene located in `Assets/Scenes/`.
3. Press **Play** in the Unity Editor.
