# Graph Report - Scripts  (2026-09-12)

## Corpus Check
- Corpus is ~2,247 words - fits in a single context window. You may not need a graph.

## Summary
- 166 nodes · 286 edges · 9 communities (8 shown, 1 thin omitted)
- Extraction: 90% EXTRACTED · 10% INFERRED · 0% AMBIGUOUS · INFERRED: 28 edges (avg confidence: 0.81)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Combat Loop & Encounter Spawning
- Card Play & Input Routing
- Combatant Damage/Block Core
- Card Data & Action Definitions
- Scheduled Event System
- Deck/Hand Draw Pipeline
- Status Effect Type & Display
- Card Effect Base Classes
- HP Bar Display

## God Nodes (most connected - your core abstractions)
1. `Combatant` - 32 edges
2. `CombatManager` - 28 edges
3. `Card` - 20 edges
4. `StatusEffectType` - 16 edges
5. `HandManager` - 16 edges
6. `Enemy` - 13 edges
7. `DeckManager` - 12 edges
8. `IScheduledEvent` - 9 edges
9. `CardData` - 9 edges
10. `CardInstance` - 9 edges

## Surprising Connections (you probably didn't know these)
- `Combatant` --references--> `StatusEffectType`  [EXTRACTED]
  MBScripts/Combatants/Combatant.cs → Enums/StatuseffectType.cs
- `AppliedEffectEntry` --references--> `StatusEffectType`  [EXTRACTED]
  NotMBclasses/CardActions.cs → Enums/StatuseffectType.cs
- `ApplyStatusEffect` --references--> `StatusEffectType`  [EXTRACTED]
  NotMBclasses/CardEffects.cs → Enums/StatuseffectType.cs
- `CombatManager` --references--> `IScheduledEvent`  [EXTRACTED]
  Managers/CombatManager.cs → Interfaces/IScheduledEvent.cs
- `Enemy` --implements--> `IScheduledEvent`  [EXTRACTED]
  MBScripts/Combatants/Enemy.cs → Interfaces/IScheduledEvent.cs

## Import Cycles
- None detected.

## Communities (9 total, 1 thin omitted)

### Community 0 - "Combat Loop & Encounter Spawning"
Cohesion: 0.09
Nodes (16): CombatManager, CurrentTime, List, TMP_Text, GameObject, List, RectTransform, EncounterManager (+8 more)

### Community 1 - "Card Play & Input Routing"
Cohesion: 0.10
Nodes (13): ContextMenu, GameObject, RectTransform, HandManager, Instance, TargetSelectionManager, Instance, Card (+5 more)

### Community 2 - "Combatant Damage/Block Core"
Cohesion: 0.13
Nodes (6): Dictionary, HashSet, IPointerClickHandler, Combatant, CurrentHP, PointerEventData

### Community 3 - "Card Data & Action Definitions"
Cohesion: 0.10
Nodes (16): CardType, Attack, Power, Skill, AppliedEffectEntry, CardPropertyFlags, Exhaust, FrontOfDraw (+8 more)

### Community 4 - "Scheduled Event System"
Cohesion: 0.13
Nodes (11): Action, IScheduledEvent, NextTime, Priority, OneShotEvent, NextTime, Priority, PeriodicEffectEvent (+3 more)

### Community 5 - "Deck/Hand Draw Pipeline"
Cohesion: 0.23
Nodes (5): ContextMenu, List, DeckManager, CardInstance, EffectiveProperties

### Community 6 - "Status Effect Type & Display"
Cohesion: 0.15
Nodes (10): StatusEffectType, Frailty, Regen, Stagger, Strength, Toughness, Vulnerable, Weak (+2 more)

### Community 7 - "Card Effect Base Classes"
Cohesion: 0.43
Nodes (4): ApplyStatusEffect, BlockEffect, CardEffect, DamageEffect

## Knowledge Gaps
- **32 isolated node(s):** `Attack`, `Skill`, `Power`, `Strength`, `Weak` (+27 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 73 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **1 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `CombatManager` connect `Combat Loop & Encounter Spawning` to `Card Play & Input Routing`, `Combatant Damage/Block Core`, `Card Data & Action Definitions`, `Scheduled Event System`, `Card Effect Base Classes`?**
  _High betweenness centrality (0.315) - this node is a cross-community bridge._
- **Why does `Combatant` connect `Combatant Damage/Block Core` to `Combat Loop & Encounter Spawning`, `Card Play & Input Routing`, `Card Data & Action Definitions`, `Scheduled Event System`, `Status Effect Type & Display`, `Card Effect Base Classes`, `HP Bar Display`?**
  _High betweenness centrality (0.302) - this node is a cross-community bridge._
- **Why does `Card` connect `Card Play & Input Routing` to `Combat Loop & Encounter Spawning`, `Combatant Damage/Block Core`, `Card Data & Action Definitions`, `Deck/Hand Draw Pipeline`?**
  _High betweenness centrality (0.246) - this node is a cross-community bridge._
- **What connects `Attack`, `Skill`, `Power` to the rest of the system?**
  _32 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Combat Loop & Encounter Spawning` be split into smaller, more focused modules?**
  _Cohesion score 0.08712121212121213 - nodes in this community are weakly interconnected._
- **Should `Card Play & Input Routing` be split into smaller, more focused modules?**
  _Cohesion score 0.09686609686609686 - nodes in this community are weakly interconnected._
- **Should `Combatant Damage/Block Core` be split into smaller, more focused modules?**
  _Cohesion score 0.12648221343873517 - nodes in this community are weakly interconnected._