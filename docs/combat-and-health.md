# Combat & health

The combat loop is three small pieces that only know each other through an
interface and events — weapons never reference crates, bars, or NPCs directly.

```
weapon raycast ──> IDamageable.TakeDamage(damage, hitPoint, hitNormal)
                        │ (HealthSystem)
                        ├─ OnHealthChanged(current, max) ──> WorldSpaceHealthBar
                        └─ OnDeath(hitPoint, hitNormal) ───> Breakable.Shatter
                                                             DeathDespawn.Destroy
```

## IDamageable (`Scripts/Core/IDamageable.cs`)

One method: `TakeDamage(float damage, Vector3 hitPoint, Vector3 hitNormal)`.
Weapons find it with `GetComponentInParent<IDamageable>()` on whatever collider
the ray hit, so child colliders on complex objects work automatically.

## HealthSystem (`Scripts/Core/HealthSystem.cs`)

The one concrete implementation: `maxHealth`, `CurrentHealth`, `IsDead`, plus
two events — `OnHealthChanged(current, max)` after every change and
`OnDeath(hitPoint, hitNormal)` once, carrying the killing blow's contact data
so reactions can be directional (crate shards fly away from the hit).

Reactions subscribe in `Awake`:

- **Breakable** — shrinks slightly per hit, shatters on death. It
  `[RequireComponent]`s a HealthSystem, so adding `Breakable` in code or in the
  Inspector automatically brings health along.
- **DeathDespawn** — placeholder death for NPCs: object vanishes. Replace with
  ragdoll/animation later; nothing else needs to change.

Current numbers: crates 50 hp, villain 100 hp, crowbar 25 dmg, 9mm 8 dmg —
two swings or seven bullets per crate, HL1-flavored.

## Debug health bars (`Scripts/UI/WorldSpaceHealthBar.cs`)

A world-space Canvas + non-interactable `Slider` (red background, green fill)
floating `heightOffset` above anything with a HealthSystem, built entirely from
code at runtime — there are no UI prefabs to maintain. It subscribes to
`OnHealthChanged`, billboards toward `Camera.main` every `LateUpdate`, and
destroys itself on death.

**Turning them off** (for the final immersive look):

- Globally: set `WorldSpaceHealthBar.GloballyEnabled = false` before scene load
  (or flip the `barEnabled` field per object). When disabled the component
  returns early — **no canvas, slider, or image objects are ever created**, so
  the hierarchy stays clean.

## Layers

`G1SceneBuilder` registers a `Player` layer (first free slot ≥ 8 in
TagManager), assigns the whole player hierarchy to it, and gives every weapon a
`hitMask` of everything-except-Player — so your own capsule and viewmodels can
never eat a bullet or a swing.

## Player death & respawn (`Scripts/Core/G1PlayerDeath.cs`)

Subscribes to the player HealthSystem's `OnDeath`: freezes movement, mouse
look, and all weapons; plays a pitched-down groan; fades the screen to black
over 1.5 s; then reloads the active scene. Mounted by the scene builder.
Checkpoints (save position + weapon unlocks, restore instead of full reload)
are the planned upgrade — see the roadmap.
