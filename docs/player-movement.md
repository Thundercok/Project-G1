# Player movement — the HL1 physics model

`Assets/G1/Scripts/PlayerMovement.cs` implements GoldSrc-style movement on a
`CharacterController`. It is not an approximation of the feel — it is the actual
Quake-lineage algorithm with Half-Life 1's server constants converted from
Half-Life units (1 unit = 1 inch = 0.0254 m).

## Constants

| Field | Default | HL1 equivalent |
|---|---|---|
| `maxSpeed` | 8.1 m/s | `cl_forwardspeed` ≈ 320 ups |
| `stopSpeed` | 2.5 m/s | `sv_stopspeed` 100 |
| `friction` | 4 | `sv_friction` 4 |
| `accelerate` | 10 | `sv_accelerate` 10 |
| `airAccelerate` | 10 | `sv_airaccelerate` 10 |
| `airWishCap` | 0.76 m/s | the famous 30-ups air wish clamp |
| `gravity` | 20.3 m/s² | `sv_gravity` 800 |
| `jumpSpeed` | 6.8 m/s | ≈ 45-unit jump apex |

## How a frame works

**Grounded:**
1. **Friction** — speed decays by `max(speed, stopSpeed) * friction * dt`.
   The `stopSpeed` floor is why you stop crisply at low speed instead of
   sliding on ice forever.
2. **Accelerate** toward the input direction (see formula below) up to `maxSpeed`.
3. Holding **Jump** launches immediately — there is no landed-frame friction
   window, which is what makes bunnyhopping keep its speed.

**Airborne:**
1. Same accelerate formula, but the wish speed is clamped to `airWishCap`
   (0.76 m/s ≈ 30 ups).
2. Gravity.

## The accelerate formula (why strafing works)

```
current    = dot(horizontalVelocity, wishDir)
add        = wishSpeed - current
accelSpeed = min(accel * wishSpeed * dt, add)
velocity  += wishDir * accelSpeed
```

Only the **projection** of velocity onto the wish direction is capped. In the air,
`wishSpeed` is a tiny 0.76 m/s — but if you strafe sideways while turning the mouse,
the wish direction stays nearly perpendicular to your velocity, `current` stays
small, and the engine happily keeps adding speed. That's air-strafing, straight
from Quake, and combined with hold-to-jump it produces classic bunnyhop
acceleration.

## Tuning cheatsheet

- Feels slippery on the ground → raise `friction` (5–6) or lower `maxSpeed`.
- Bhop gains too fast → lower `airWishCap` (0.5) or `airAccelerate`.
- Kill bunnyhopping entirely → use `Input.GetButtonDown` instead of `GetButton`
  in the jump check, and optionally cap horizontal speed on landing.
- Jump feels floaty → raise `gravity` and `jumpSpeed` together (keep
  `jumpSpeed² / (2 * gravity)` ≈ desired jump height in meters).

`MouseLook.cs` is deliberately separate: yaw rotates the player body (so movement
axes follow), pitch rotates only the camera, clamped at ±89°.
