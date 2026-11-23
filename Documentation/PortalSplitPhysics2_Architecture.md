# Portal Split Physics 2.0 — Revised Architecture (Architecture C)

> Investigation and design only. No runtime code changes. No tests modified.

## 1. Current Architecture (unchanged baseline)

See prior sections: `PortalTraverser` performs instantaneous teleport of a single `Rigidbody` using portal-relative frame transforms; `PortalSplitTransition` provides a **kinematic, collider-disabled visual proxy** with no genuine physics. Nothing here changes that. Architecture C is a *proposal* to add contact awareness on the far side while retaining one authoritative body.

---

## 2. Unity Collision Authority (critical)

### 2.1 What a kinematic shadow actually does
A kinematic `Rigidbody` (`isKinematic = true`) is **not integrated by the solver**, but it **does participate in collision detection** and **does generate contact reports** (`OnCollisionEnter/Stay/Exit`) when its colliders overlap dynamic bodies. Contacts between two kinematic bodies, or a kinematic and a static collider, are reported only if at least one side is dynamic? Unity reports collisions where at least one body is dynamic or the callback is requested; kinematic-vs-static contacts are generally **not** reported unless `CollisionDetectionMode`/callbacks configured. In practice, `OnCollisionEnter` on a kinematic body fires when it overlaps a **dynamic** body, and the dynamic body receives the impulse from the solver. The kinematic body itself receives no velocity change.

### 2.2 Double-counting risk
If the shadow's collider overlaps a dynamic body, the solver **already applies an impulse to that dynamic body** (pushing it away from the shadow). If the impulse bridge *also* applies an equal/opposite impulse to the authoritative body, the dynamic body gets pushed twice (once by solver contact with shadow, once by... actually the authoritative body is not in contact with it, so the dynamic body is only pushed by the shadow). The risk is the *reverse*: the authoritative body never feels the contact, so we must inject a reaction into it. But the dynamic body already reacted to the shadow. Therefore:

- The shadow is the **collision authority on the destination side** for generating contacts with other bodies.
- The authoritative body is the **motion authority** and receives a **reflected reaction** so its trajectory accounts for the contact.
- We must **not** also apply an impulse to the dynamic body from the bridge (the solver already did). The bridge only updates the authoritative body.
- To avoid the solver applying a *second* correction when the authoritative body later teleports into the same region, the straddle completes with the standard instantaneous traversal, at which point the shadow is destroyed and the authoritative body takes over at the exit frame.

### 2.3 Ownership
| Side | Motion authority | Collision detection authority |
|------|------------------|------------------------------|
| Source | Authoritative body | Authoritative body (native) |
| Destination (straddling) | Authoritative body (via bridge) | Shadow (kinematic, reports contacts) |
| After completion | Authoritative body at exit | Authoritative body (native) |

---

## 3. Contact Detection Coverage

| Collider type | Callback sufficient? | Notes |
|---|---|---|
| Static colliders (walls/floors) | **No** for kinematic-vs-static | `OnCollisionEnter` typically does **not** fire for kinematic-vs-static. Must use explicit queries (`Physics.OverlapBox/Sphere/Capsule` or `ComputePenetration`) against static geometry each step. |
| Dynamic Rigidbody | Yes (callback fires) | Shadow vs dynamic → callback; impulse available per contact via `ContactPoint.impulse` (Unity 2022.1+). Aggregate `Collision.impulse` is unreliable for angular/multi-contact use. |
| Kinematic Rigidbody | Partial | Kinematic-vs-kinematic contacts are reported only in some configurations; treat as unreliable; use overlap queries. |
| Moving/rotating geometry | Callbacks fire but stale | Because the shadow is teleported, contacts are re-evaluated each step; persistent contact IDs are not stable across teleport. Treat each step independently. |

**Conclusion:** The bridge must combine `OnCollisionEnter/Stay/Exit` (for dynamic contacts, where impulse is available) **plus** explicit `Physics.ComputePenetration` / overlap queries against static and kinematic geometry. Do not rely solely on callbacks.

---

## 4. Collider Partitioning

The shadow must represent only the **destination-side portion** of the object. Cloning the entire collider set would make the shadow collide with geometry the real object has not yet reached.

### 4.1 Strategy by collider type

| Collider | Support | Approach |
|---|---|---|
| `BoxCollider` | Exact-ish | Compute the box's intersection with the portal plane in local space; create a smaller box approximating the clipped portion (AABB of the intersection). Approximation: the clipped box is an AABB in the box's local frame, not an exact frustum. |
| `SphereCollider` | Approximation | If sphere centre is on destination side, use full sphere; if straddling, use a smaller sphere centred at the clipped centre with radius = distance from clipped centre to plane. Conservative. |
| `CapsuleCollider` | Approximation | Split into two capsules? Not natively. Approximate with a shorter capsule whose endpoints are clamped to the destination side. |
| `MeshCollider` (convex) | Approximation only | Unity cannot split a mesh at runtime cheaply. Options: (a) use the full mesh collider on the shadow but gate contacts by a per-contact plane test (discard contacts whose point is on the source side); (b) pre-author split meshes. **Do not promise arbitrary runtime mesh splitting.** |
| Compound hierarchy | Per-child | Apply the above per child collider, preserving hierarchy. |

### 4.2 Contact gating (universal fallback)
Regardless of partitioning, every contact reported by the shadow is gated: if `dot(portalPlaneNormal, contactPoint - planePoint) < 0` (source side), discard it. This prevents the shadow from reacting to geometry the real object has not reached. This is the reliable baseline; partitioning improves accuracy but gating is mandatory.

---

## 5. Impulse Bridge Mathematics

### 5.1 Frames
Let entrance frame `E`, exit frame `X`, flip `F = Ry(180)`. Portal transform matrix `M = X * F * E^{-1}` (maps source-space vectors/points to destination space). Inverse `M^{-1} = E * F^{-1} * X^{-1}`.

### 5.2 Contact transform
For a shadow contact on the destination side:
- Contact point in world (destination space): `p_d`.
- Contact normal (destination space, pointing into shadow): `n_d`.
- Relative velocity of the two bodies at contact (destination space): `v_rel_d = v_shadow_at_p - v_other_at_p`.

To apply an equivalent impulse to the authoritative body (source side), transform:
- `p_s = M^{-1} * p_d`
- `n_s = M^{-1}.rotation * n_d` (normals transform as directions)
- `v_rel_s = M^{-1}.rotation * v_rel_d`

### 5.3 Impulse calculation

The collision on the destination side is solved by Unity between the **kinematic shadow** and the **other body**. The authoritative body is not in that solve. Therefore the bridge must **not** recompute a contact impulse from the authoritative body's mass/inertia and the other body's mass/inertia, and must **not** assume momentum conservation across the portal. Instead, each contact case is handled differently:

#### 5.3.1 Dynamic destination contacts (use Unity's actual impulse)

When the shadow overlaps a **dynamic** Rigidbody, Unity's solver applies a real impulse to that dynamic body. The bridge retrieves that impulse and reflects it to the authoritative body.

**Retrieval:**
- Handle contacts in `OnCollisionEnter` / `OnCollisionStay` on the shadow (or equivalently on the dynamic body, but handle in exactly one place to avoid duplicates).
- For each `ContactPoint c` (Unity 2022.1+), read `c.impulse` (Vector3) and `c.point`, `c.normal`, `c.thisCollider`, `c.otherCollider`.
- `c.impulse` is the per-contact impulse applied to the contact pair. Its **sign is inconsistent** across collider types and scene load conditions (see §2.2/§3), so it must be oriented before use.

**Sign convention (shadow's perspective):**
- Let `n_d` be the contact normal pointing **into the shadow** (from the other body toward the shadow), as defined in §5.2.
- The impulse that would have been applied to the shadow by this contact points **away from the other body**, i.e. its normal component is `>= 0` along `n_d`. Call this `J_shadow_d`.
- Orientation rule: if `dot(rawImpulse, n_d) < 0`, set `J_shadow_d = -rawImpulse`; if `> 0`, set `J_shadow_d = rawImpulse`; if `≈ 0` (tangential-only or degenerate), fall back to the relative-velocity heuristic (impulse on shadow opposes the shadow's incoming relative motion) or skip the contact.
- The impulse applied by Unity to the dynamic body is the opposite: `J_other_d = -J_shadow_d`.

**Angular effects:**
- `ContactPoint.impulse` is a **linear** impulse applied at `c.point`. Its torque on the shadow is `(c.point - COM_shadow) × J_shadow_d`. Thus angular effects are recovered **only when per-contact impulses and points are used**.
- The aggregate `Collision.impulse` does **not** retain per-contact lever arms and cannot reproduce angular effects or multi-contact distributions. Do not use it for the bridge when angular fidelity is required. If only the aggregate is available (pre-2022.1), angular reflection is unavailable and must be skipped or approximated with the COM lever arm (documented as degraded).

**Reliability:**
- Callbacks fire for kinematic-vs-dynamic contacts, so impulse is available for every such contact that is actually solved.
- Not available for: static colliders, kinematic-vs-kinematic contacts (unreliable), tunnelling misses, and contacts resolved by swept-query fallback (no Unity impulse). Those use §5.3.2 or are skipped.
- `OnCollisionStay` provides a per-step impulse; `OnCollisionExit` provides no meaningful impulse (do not apply).
- Per-contact impulse requires Unity 2022.1+. Document this version requirement.

**Application to the authoritative body (through the inverse portal transform):**
Given `J_shadow_d` (impulse that would have been applied to the shadow) and contact point `p_d`:

```
J_A = M^{-1}.rotation * J_shadow_d          (impulse vector, transformed by inverse rotation only)
p_s = M^{-1} * p_d                           (contact point in source space)
```

Apply to the authoritative Rigidbody at `p_s`:

```
rb_A.AddForceAtPosition(J_A, p_s, ForceMode.Impulse)
```

This single call updates both linear and angular velocity using `m_A` and `I_A` in the body's own frame. Equivalently:

```
Δv_COM_A = J_A / m_A
Δω_A     = I_A^{-1} * ((p_s - COM_A) × J_A)
```

**Constraints:**
- The bridge applies impulse **only to the authoritative body**. It must **never** apply an impulse to the dynamic body — Unity's solver already did. This is the "no duplicate impulse application" rule.
- No second collision impulse is computed; momentum is not assumed conserved across the portal (the portal can exchange momentum with bodies).

**Friction (dynamic):** The tangential component of `J_shadow_d` is already included in the per-contact impulse (Unity's solver includes friction). Do not recompute or cap it separately unless clamping for stability (§5.4). If per-contact impulse is unavailable, friction is not reflected.

#### 5.3.2 Static destination contacts (penetration-based response)

Static colliders provide **no dynamic collision impulse** to reflect. The bridge must compute a contact response from geometry and kinematics.

**Inputs per contact (destination space):**
- Penetration depth `d > 0` (overlap), from `Physics.ComputePenetration` or overlap queries.
- Contact normal `n_d` pointing into the shadow (aligned to our convention).
- Relative contact-point velocity `v_rel_d = v_shadow_at_p_d - v_wall_d` (portal-frame velocity; see §5.5).
- Portal-transformed quantities for the authoritative body: `m_A`, `I_A`, `COM_A`, `r_s = p_s - COM_A`.

**Effective mass along the normal (source frame, equivalent under rotation):**

```
k_s = 1/m_A + (r_s × n_s) · I_A^{-1} · (r_s × n_s)
m_eff = 1 / k_s
```

where `n_s = M^{-1}.rotation * n_d`, `r_s = M^{-1} * p_d - COM_A`.

**Impulse on the shadow (source-transformed to A):**

For an instantaneous contact, the normal impulse required to achieve restitution `e` is:

```
J_n = -(1 + e) * (v_rel_s · n_s) / k_s       // v_rel_s = M^{-1}.rotation * v_rel_d
```

with `v_rel_s · n_s < 0` when penetrating, yielding `J_n > 0`.

For **sustained/resting contact**, `v_rel_s · n_s ≈ 0` so the above gives ~0 and gravity would sink the body. Handle without callbacks (which do not fire for kinematic-vs-static) by adding a penetration bias (Baumgarte-style stabilization):

```
J_n = [ -(1 + e) * (v_rel_s · n_s) + (β * d) / dt ] / k_s
```

where `β` is a stabilization coefficient (documented as a solver-stability parameter, not a physics parameter), `d` is penetration depth, `dt` is the fixed timestep. Clamp `J_n >= 0` (never pull bodies together) and clamp `|J|` per §5.4.

**Friction (static):** tangential impulse

```
v_t = v_rel_s - (v_rel_s · n_s) n_s
J_t = -clamp( (v_rel_s · t)/k_t , -μ |J_n|, μ |J_n| ) * t   // capped at μ |J_n|
```

Simpler: compute tangential impulse `J_t_mag = - (v_rel_s · t) / k_t` where `k_t` uses the same formula with `t` instead of `n`, then clamp `|J_t_mag| <= μ |J_n|`. Apply along `t`.

**Application:**

```
J_s = J_n * n_s + J_t * t_s        // already in source space
p_s = M^{-1} * p_d
rb_A.AddForceAtPosition(J_s, p_s, ForceMode.Impulse)
```

Note: `J_s` above is already in source space (computed with `n_s`, `v_rel_s`); apply it directly at `p_s` without further transform.

**Sustained contact handling:**
- Re-run `Physics.ComputePenetration` / overlap queries **every FixedUpdate**; do not rely on `OnCollisionStay` (does not fire for kinematic-vs-static).
- Each step independently computes and applies an impulse. Resting contact therefore produces a small impulse each step that balances gravity and penetration bias, approximating a sustained normal force.
- Persist contact identity across steps by collider pair + contact point proximity (not by callback IDs, which are unstable across teleports).

#### 5.3.3 No duplicate impulse application

- A contact handled by the dynamic path (§5.3.1) must be marked processed for that step and not re-handled by static queries.
- A contact handled by static path (§5.3.2) must not also be treated as dynamic.
- The bridge applies impulse **only** to the authoritative body; Unity's solver owns the other body.

### 5.4 Why no arbitrary coupling coefficient
The impulse magnitude above is derived from the contact mechanics. The only scaling is the physically meaningful restitution `e` and friction `μ`. No ad-hoc "coupling coefficient" should be used to mask incorrect transforms. If numerical safety requires clamping, clamp `|J|` to a per-step maximum derived from the body's mass and a configured max acceleration (e.g., `J_max = m_A * a_max * dt`), and document that this is a **stability clamp**, not a physics parameter. Clamping may cause slightly inelastic behaviour at extreme contacts, which is acceptable and must be logged.

### 5.5 Moving/rotating portals

`M` and `M^{-1}` are recomputed each `FixedUpdate` from current portal poses. Portal linear/angular velocities must be accounted for in both shadow velocity and contact response:

**Shadow velocity (destination space):**
```
v_shadow_lin = TransformVelocity(v_A, v_E, v_X, q_E, q_X)        // PortalMath.TransformVelocity
ω_shadow     = TransformAngularVelocity(ω_A, ω_E, ω_X, q_E, q_X) // PortalMath.TransformAngularVelocity
COM_shadow_d = M * COM_A                                         // transformed COM (approx; mass not partitioned)
v_shadow_at_p_d = v_shadow_lin + ω_shadow × (p_d - COM_shadow_d)
```
where `v_E, ω_E` and `v_X, ω_X` are the entrance/exit portal linear and angular velocities.

**Relative contact velocity:**
```
v_rel_d = v_shadow_at_p_d - v_other_d          // v_other_d = other body's world velocity (0 for static)
v_rel_s = M^{-1}.rotation * v_rel_d
```

**Impulse transform:** Impulses are transformed by rotation only:
```
J_A = M^{-1}.rotation * J_shadow_d
```
Portal linear velocity does not appear in the impulse transform (impulse is momentum, not velocity). Portal angular velocity affects `v_shadow` and hence the computed static impulse, but not the transform of a Unity-measured impulse.

**Degraded mode:** If `|M - M_prev|` (rotation or translation delta) exceeds a threshold between steps, **skip reflection for that step** to avoid phantom impulses from discontinuous frame changes. Also skip if portal angular velocity is so large that the small-dt assumption for impulse transform breaks down. Document as limitation.

---

## 6. Collision Timing and Tunnelling

- The shadow is teleported each `FixedUpdate` by possibly large distances. Unity's solver does **not** sweep the shadow between poses; contacts are only detected at the new pose. This means fast straddling objects or fast-moving portals can **miss contacts** (tunnelling through thin geometry).
- Mitigation: run `Physics.SphereCast`/`BoxCast` along the shadow's displacement vector between previous and current pose for each collider, feeding synthetic contacts into the bridge. This is an approximation and adds cost.
- Contact persistence: because the shadow teleports, `OnCollisionStay` will not fire reliably across a teleport; treat each step as independent contact detection. Do not depend on stay callbacks for sustained forces; accumulate impulses each step from fresh queries.

---

## 7. Revised Implementation Stages (independently testable)

### Stage 0 — Instrumentation only
- Add Profiler markers; no behaviour change. Validate baseline.

### Stage 1 — Shadow creation and pose tracking (no physics)
- Create kinematic shadow with collider clones (gated by plane).
- Teleport shadow each `FixedUpdate`.
- **No impulse application.**
- Test: shadow pose matches portal transform; cleanup on destroy.

### Stage 2 — Contact detection only (no impulses)
- Add callback + overlap query detection.
- Log contacts; verify against static/dynamic/kinematic.
- Test: detection accuracy, no missed static walls.

### Stage 3 — Static environment interaction
- Apply reflected impulses for contacts with static geometry only.
- Test: shadow hitting a static wall slows/deflects authoritative body appropriately.

### Stage 4 — Dynamic object interaction
- Apply reflected impulses for dynamic contacts, including impulse magnitude from callbacks and queries.
- Test: dynamic body pushing shadow moves authoritative body; no double-counting.

### Stage 5 — Collider partitioning
- Implement per-collider-type clipping (§4).
- Test: partitioned shadow does not collide with geometry behind plane.

### Stage 6 — Edge cases and hardening
- Moving portals, reversal, high speed, multi-object, sustained contact.

### Stage 7 — Completion integration
- When fully across, perform standard traversal teleport and destroy shadow.

Each stage must have passing tests before proceeding.

---

## 8. Test Plan (additions)

### 8.1 Existing/retained
- Shadow vs static wall: detection + deflection.
- Shadow vs dynamic Rigidbody: impulse reflected, dynamic body pushed by solver only.
- Shadow vs kinematic Rigidbody: detection via queries.
- Simultaneous source + destination contacts: both reflected, no duplication.
- Duplicate impulse prevention: same contact not applied twice per step.
- Contact-point transform: `p_s = M^{-1} p_d`, normal transform.
- Angular impulse/torque: `Δω = I^{-1}(r × J)`.
- Moving portals: shadow tracks; degraded mode on large delta.
- Reversal mid-straddle: progress decreases, shadow updates.
- Collider partitioning: box/sphere/capsule/mesh gating.
- High-speed: swept queries mitigate tunnelling (best-effort).

### 8.2 Mathematical tests (EditMode, pure math, no scene)
- **Impulse sign:** Given `rawImpulse` with negative normal component relative to `n_d`, orientation yields `J_shadow_d` with non-negative normal component. Verify with representative vectors.
- **Linear momentum transfer (static formula):** For head-on contact (`v_rel·n < 0`, `r × n = 0`), `J_n = -(1+e)(v_rel·n)/ (1/m_A)`; authoritative `Δv = J_n/m_A * n_s` (source). Verify numeric.
- **Angular momentum transfer:** Off-center contact with diagonal inertia tensor; verify `Δω = I^{-1}(r × J)` and that `ΔL = r × J`.
- **Effective mass:** For a body with inertia, `k = 1/m + (r×n)·I^{-1}·(r×n)`; verify reduction to `1/m` when `r` parallel to `n`.
- **Contact transform round-trip:** `p_s = M^{-1} p_d`, then `M p_s = p_d`; normal transforms as direction; velocity transform with portal velocities reduces to `R v` for static portals.
- **Moving portal velocity transform:** `TransformVelocity` and `TransformAngularVelocity` match formulas including `v_E`, `v_X`.

### 8.3 Dynamic destination collision tests (PlayMode)
- **Dynamic impulse reflection:** Shadow hits dynamic body; read `ContactPoint.impulse`, orient, transform; authoritative body velocity/ω change matches `J_A/m_A` and `I^{-1}(r×J_A)` within tolerance.
- **No double impulse on dynamic body:** Dynamic body's velocity change is attributable solely to Unity solver (measure with bridge disabled vs enabled; dynamic body trajectory identical).
- **Angular from per-contact impulse:** Off-center hit on dynamic body produces measurable angular change in authoritative body; aggregate impulse path does not (documented).
- **Sign robustness:** Impulse orientation rule produces consistent results across collider types (box/sphere/capsule) and hot/cold scene loads.
- **Callback coverage:** Verify `OnCollisionEnter`/`Stay` fire for kinematic shadow vs dynamic body and impulse non-zero while in contact; `OnCollisionExit` impulse ignored.

### 8.4 Static destination collision tests (PlayMode)
- **Static wall deflection:** Shadow vs static wall slows/deflects authoritative body; penetration depth decreases after impulse.
- **Sustained contact:** Resting shadow against static floor over N steps produces repeated small impulses balancing gravity; no callback required; no sinking beyond tolerance.
- **Friction cap:** Tangential impulse magnitude <= `μ |J_n|`.
- **Penetration bias:** With `v_rel·n ≈ 0` but `d > 0`, impulse positive due to `β d / dt` term.

### 8.5 Moving portal tests (PlayMode)
- **Shadow tracks moving portals:** Shadow pose follows `M` each FixedUpdate.
- **Degraded mode:** Large per-step `M` delta skips reflection; no phantom impulse; logged.
- **Portal velocity in v_shadow:** Moving entrance/exit portals produce correct relative velocity and impulse.

### 8.6 No duplicate impulse application tests
- **Callback + query dedup:** A contact reported by both callback and overlap query is applied once.
- **Dynamic vs static classification:** A contact is not handled by both paths.
- **Single owner:** Bridge never calls `AddForce` on the other (dynamic) body.

---

## 9. Performance Methodology

The previous 0.1–0.5 ms estimate is **unverified**. Required measurements:

- Benchmark scene: N straddling objects (N = 1, 5, 10, 50), M portals (1, 4), static + dynamic contacts.
- Measure: `PortalSplitController` CPU time per `FixedUpdate` (Profiler marker), GC allocations (Profiler, `GC.Alloc`), physics step time (Unity Profiler Physics module), contact count, impulse count.
- Report min/avg/max over 600 frames at 50 Hz fixed timestep.
- Identify bottleneck (queries vs callbacks vs transforms) before optimising.

---

## 10. Limitations (retained and expanded)

1. Not true two-sided physics: shadow reports contacts; sustained resting contact is approximated by per-step impulses, not natively solved.
2. Mass, inertia, COM not partitioned (single body).
3. Joints cannot span portals.
4. CharacterController unsupported (instant teleport only).
5. Friction/restitution on far side estimated, not natively solved.
6. High-speed tunnelling possible despite swept queries (best-effort).
7. MeshCollider splitting is not performed; contact gating is the fallback.
8. Moving portals may skip reflection on large per-step deltas.
9. Clamping may cause slightly inelastic behaviour at extreme contacts.
10. Dynamic impulse fidelity depends on Unity's per-contact impulse API (2022.1+); on older versions angular effects and multi-contact distribution are unavailable.
11. `Collision.impulse` aggregate sign is unreliable; bridge relies on per-contact impulse plus normal/velocity orientation heuristics.
12. Portal can exchange momentum with bodies, so total world momentum is not guaranteed conserved.
13. Static contact response uses a stabilization bias (`β`) that is a solver parameter, not a physical property.

---

## 11. Viability and First Stage

**Is Architecture C viable?** Yes, as a *contact-aware approximation*, provided expectations are set correctly: it improves far-side interaction (push/pull/deflection) but does not deliver true two-body resolution. It is superior to A (no far-side physics) and B (unstable multi-body) for this codebase.

**First implementation stage:** Stage 1 — shadow creation and pose tracking with **no impulse application**, validated by pose/cleanup tests. Only after Stage 2 (contact detection) validates detection accuracy should any impulse be applied (Stage 3, static only).

---

## 12. Summary of Design Changes vs. Prior Revision

- Clarified kinematic shadow collision authority and double-counting prevention.
- Replaced callback-only assumption with callback + explicit query strategy.
- Added per-collider-type partitioning table with exact vs. approximate support; explicit statement that arbitrary MeshCollider splitting is not promised.
- Replaced arbitrary coupling coefficient with derived impulse formula; clamping redefined as stability-only.
- Added contact transform math through portal frames.
- Added tunnelling/timing analysis and swept-query mitigation.
- Reordered implementation into independently testable stages starting with detection only.
- Expanded test plan and defined unverified performance benchmark methodology.
- Retained all prior limitations and added new ones.

---

## 13. Dynamic vs. Static Contact Cases — Summary

**Dynamic destination contacts** are solved by Unity between the kinematic shadow and the dynamic body. The solver produces a real impulse on the dynamic body, available per contact via `ContactPoint.impulse` (Unity 2022.1+). The bridge does **not** compute a second impulse; it retrieves Unity's impulse, orients it to the shadow's perspective using the contact normal and relative velocity, transforms it through the inverse portal, and applies it **only** to the authoritative body (`AddForceAtPosition`). The dynamic body is never touched by the bridge. Angular effects are recovered from per-contact lever arms. This case relies on callbacks and is subject to sign ambiguity and version availability.

**Static destination contacts** are not solved by Unity (kinematic-vs-static produces no impulse and no callback). The bridge must compute its own response from penetration depth, contact normal, relative contact-point velocity, the authoritative body's effective mass, inertia, and lever arm, using a penetration-biased impulse formula. Sustained contact is handled by re-running penetration queries every `FixedUpdate` and applying a small impulse each step. Friction is capped by `μ |J_n|`. This case is fully computed by the bridge and is inherently an approximation, with a stabilization bias for resting contact.

**Key difference:** Dynamic case *reflects a measured impulse* (no recomputation, no momentum assumption); static case *computes an impulse* from geometry and kinematics (approximate, with stabilization). Both apply the result only to the authoritative body through the inverse portal transform, and neither applies impulse to the other body.