# Prototype notes — overlay variants

**Skill:** prototype  
**Page:** `DesignLabPage` (nav: Design lab)  
**Question:** What should the prayer overlay look like before Phase 4?

| Variant | Idea | Strength | Weakness |
|---------|------|----------|----------|
| **A** | Top ribbon (toast-like) | Familiar, unobtrusive | Easy to miss during work |
| **B** | Bottom dock card | Strong presence, good for adhan stop | Covers taskbar / dock area |
| **C** | Start-edge callout | Distinctive, Maghreb “bookmark” feel | Needs RTL flip to end-edge |

**Default lean:** B for prayer-start + adhan; A for pre-alert only.  
**Latin digits:** all mock times use `LatinDigitsTextBlock`.  
**Phase 4:** production surface is `OverlayService` / `OverlayWindow` (B @ start, A @ pre-alert). Design lab kept for motion experiments + “Fire overlay+audio”.
