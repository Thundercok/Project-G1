# Project G1 — Story Bible

An original story in the spirit of 1998: an ordinary scientist, a catastrophic
experiment, a government that sends soldiers instead of rescuers, and a
stranger in a suit who is always, always watching.

## Setting

**The Corvus Deep Research Annex** — a subterranean materials-science facility
built into a decommissioned missile silo complex. Officially it studies exotic
lattice materials. Unofficially, Sub-Level C hosts **Project G1**: an attempt
to hold open a microscopic aperture to *somewhere else* long enough to sample
it. The aperture is called the **Threshold**.

## Characters

- **Dr. Dang** (the player) — a junior test engineer in an orange Class-IV
  hazard suit with a Λ calibration emblem. Wrong seniority to refuse the
  morning's test. Right suit to survive it.
- **The Auditor** (the villain model) — a gaunt man in a dark suit with a
  briefcase who appears in places no one could reach, observes, and is gone
  when you look twice. His employer predates the Annex. His interest predates
  Dr. Dang.
- **The Sweepers** (HECU-style soldiers) — a military containment unit whose
  orders are not evacuation. Their word for witnesses is "residue".
- **The taken** (zombies) — Annex staff, changed by what came through.
- **The strays** (aliens) — fauna from the far side of the Threshold, spilling
  through wherever the aperture's echo is strong.

## The Threshold Event

During a routine widening test, the Threshold refuses to close — something on
the far side is holding the door. The aperture's echo propagates through the
Annex like a struck bell: containment fails, staff are taken, and the strays
begin to arrive. Topside, the response protocol is activated. It is not a
rescue protocol.

## Chapters (one per level)

### Chapter One — COLD START (Level 1, `TestScene`)
Dr. Dang survives the event in the Sub-Level C locker room. Escape through
the lab corridor, the control room, and the industrial hall — first past the
taken, then past the strays, and finally past the first Sweepers, who make
the situation clear by opening fire. The emergency elevator leads up.
*The Auditor watches from behind the glass in the control room.*

### Chapter Two — QUARANTINE (Level 2, `Level2`)
The elevator opens on the surface motor pool at dawn: overcast, silent, and
cordoned. A Sweeper helicopter idles on the pad; patrols hold the courtyard.
Nothing gets out — so Dr. Dang goes back down, through the maintenance shaft
on the far side of the yard, into the oldest part of the complex.
*The Auditor stands on the perimeter wall, unbothered by the patrols.*

### Chapter Three — THRESHOLD (Level 3, `Level3`)
The Undercroft: the original silo chamber, now grown over with pods and spore
light. The Threshold hangs at its center — a ring of light the strays pour
through. There is no closing it from this side. There is only stepping
through and closing it from *that* side.
*The Auditor waits beside the ring. For the first time, he seems pleased.
He does not stop you. He opens the briefcase.*

**Ending card:** *"SUBJECT: DANG — ASSESSMENT COMPLETE. DISPOSITION:
RETAINED."*

## Delivery mechanics (all in-engine, no VO)

- **Chapter cards** (`G1StoryCard`) — HL1-style typewriter title cards on
  level start; also trigger-volume mode for mid-level beats and the ending.
- **Auditor cameos** (`G1GManCameo`) — the villain model placed in
  unreachable spots; faces the player, vanishes when approached.
- **Terminals** (`G1Terminal`, existing) — lore logs from Annex staff.
- **Level flow** — `G1LevelExitTrigger.nextScene`: TestScene → Level2 →
  Level3 → MenuScene (ending).
