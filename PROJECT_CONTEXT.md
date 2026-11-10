# PROJECT_CONTEXT.md
# Kamer vol spoken — Complete Game Project Context

## 1. Purpose of This Document

This document describes our entire Unity VR horror game project, **Kamer vol spoken**, also known internally as **House of Ghosts**.

It is intended to help Gemini understand our game, gameplay systems, assets, user stories, technical requirements, priorities, and team responsibilities.

Use this document together with `GEMINI.md`.

`GEMINI.md` defines how you must work with me. This document describes what we are building.

**Important:** This document is a design specification, not proof that any feature has already been implemented.

Never assume a system, asset, scene, prefab, or script already exists just because it is mentioned here.

Inspect our actual Unity project before making recommendations.

Do not modify the project automatically. Follow the permission and manual implementation rules in `GEMINI.md`.

---

# 2. General Project Information

**Project name:** Kamer vol spoken  
**Internal English name:** House of Ghosts  
**Genre:** VR horror, suspense, exploration, ghost hunting  
**Game engine:** Unity 6.3 LTS  
**Render pipeline:** Universal Render Pipeline (URP)  
**Target device:** Meta Quest 3, standalone VR  
**Programming language:** C#  
**Team size:** Four students  
**Team roles:** Two programmers and two artists/designers  
**Collaboration:** GitHub and Trello  
**Unity integration:** CoplayDev MCP  
**Design tools:** Figma and Mobbin

Our game is a first-person VR horror experience set inside an old, dark, abandoned house.

The player searches for a ghost using a flashlight, spatial audio, and interactions with the environment.

The ghost is a young girl with black hair and a white nightgown.

The game focuses on tension, atmosphere, sound, and exploration instead of jumpscares.

The player must find and capture the same ghost six times.

Each round becomes more difficult.

Our goal is to create a visually attractive, optimized, comfortable standalone VR experience.

The art direction uses a mid-poly style.

---

# 3. Core Gameplay

The main gameplay is based on finding, chasing, and capturing a ghost.

The gameplay loop is:

1. The player explores the house.
2. The player interacts with the environment.
3. Ghost activity is triggered through exploration.
4. The ghost hides, moves, and occasionally makes sounds.
5. The player listens to spatial audio to locate the ghost.
6. The ghost becomes visible when she comes out of hiding.
7. The player points the flashlight toward the ghost.
8. The player must keep the ghost illuminated for three continuous seconds.
9. If successful, the ghost is captured.
10. The capture counter increases by one.
11. The next round begins automatically.
12. The ghost becomes faster, smarter, and more difficult to find.
13. This continues until six captures are completed.

If the player loses the ghost during a capture attempt, the ghost runs away and hides again.

The flashlight remains the main capture mechanic.

The Holy Pistol is another required gameplay mechanic, but it must not replace the flashlight.

---

Section 4 — Player and VR Systems

Using equipment with both hands

The player can hold the flashlight in one hand and the Holy Pistol in the other hand at the same time.

Both items must support independent VR controller interactions.

The invisible belt is used to store and retrieve important equipment.

The player should be able to switch between holding, storing, and retrieving items naturally in VR.

Do not assume that an inventory system is already implemented.

## Movement

The game supports:

- Normal walking.
- Teleportation.
- Snap turning.
- Comfortable VR movement.
- Seated and standing gameplay.

The player can access teleportation through the Pause Menu.

Never introduce forced camera movements.

## Invisible belt inventory

The player has an invisible inventory belt near the hips.

Important objects can be stored and retrieved through this system.

The flashlight is initially stored on the belt.

The player must retrieve the flashlight before beginning the main ghost-search gameplay.

The belt should feel natural to use with VR hands.

Do not build a full-body inventory system.

## Flashlight recovery

The flashlight is a physical VR object.

The player can pick it up, hold it, turn it on or off, and store it.

If the flashlight is dropped, the player must find it again.

When it is far away, it should become highlighted.

Do not automatically return the flashlight to the player's hand unless we approve that behaviour.

---

# 5. Game Rooms and Environment

The game contains three rooms:

## Living Room

An old living room with vintage furniture.

Potential objects include:

- Old sofa.
- Coffee table.
- Television.
- Cupboards.
- Lamps.
- Carpets.
- Pillows.
- Curtains.
- Picture frames.
- Decorative objects.

## Bedroom

An old girl's bedroom.

Potential objects include:

- Wooden bed.
- Pink bedding.
- Nightstand.
- Wardrobe.
- Toys.
- Dolls.
- Teddy bear.
- Toy box.
- Curtains.
- Family photographs.

## Attic

A dusty attic with old objects, hiding spots, and a mysterious atmosphere.

The Attic is locked at the beginning.

It becomes available after the third successful ghost capture.

### Environment style

The house should feel:

- Old.
- Dusty.
- Abandoned.
- Dark.
- Mysterious.
- Uncomfortable but not dependent on jumpscares.

Use moonlight, environmental lighting, sound, and shadows to create tension.

Visual quality must be balanced with Quest 3 performance.

---

# 6. Menus and Game Start

## Main Menu

When the application starts, the player sees the Main Menu.

Required buttons:

- Start.
- Settings.
- Quit.

Selecting Start opens the Chapter Selection menu.

## Chapter Selection

Currently, only Chapter 1 is available.

Do not assume additional chapters have been planned.

Selecting Chapter 1 opens the Difficulty Selection menu.

## Difficulty Selection

The player chooses:

- Easy.
- Normal.
- Hard.

The chosen difficulty affects ghost movement speed and Danger Meter behaviour.

The exact numerical differences have not yet been decided.

## Room Selection

After selecting a difficulty, the player chooses a starting room:

- Living Room: Available.
- Bedroom: Available.
- Attic: Locked.

The player starts in the chosen available room.

The flashlight is initially stored on the invisible belt.

Gameplay starts directly without a required story introduction.

A short introduction is only a Could feature.

---

# 7. Ghost Character

There is ONE main ghost character.

The ghost is a young girl, approximately 12 years old.

### Appearance

- Mid-poly art style.
- Black hair.
- White nightgown.
- Recognizable silhouette.
- Clearly visible when illuminated.

Ghost behaviour

The game contains one ghost character: a young girl with black hair and a white nightgown.

The ghost must be capable of:

Hiding in different locations throughout the house.

Moving between hiding spots.

Luring the player using spatial audio.

Running away when discovered.

Moving through walls.

Occasionally making sounds such as whispering, giggling, and calling.

Becoming faster, smarter, and more difficult throughout the six rounds.

Fake Escape — Must

Fake Escape is a confirmed core gameplay mechanic.

When the ghost leaves a hiding spot, she can react in two different ways.

Behaviour A — Watch the Player

The ghost briefly stops and looks directly at the player.

This creates a short moment of tension and gives the player an opportunity to react.

After this moment, the ghost may attempt to escape.

Behaviour B — Immediate Escape

The ghost immediately runs away after leaving her hiding spot.

She does not stop to look at the player.

The player must react quickly to locate and capture her.

Behaviour selection

The ghost can choose between these two behaviours to make encounters less predictable.

The behaviour can vary between encounters.

The exact probability of each behaviour and the duration of the stopping animation have not been determined.

Do not invent these values without approval.

Moving through walls

The ghost can move through walls as part of her supernatural movement.

This can be used when escaping or moving between hiding locations.

The ghost does not always need to use doors or normal walking routes.

The movement should remain understandable to the player through sound and other appropriate feedback.

The game must not use jumpscares.

### Ghost visibility

The player visually encounters the ghost when she leaves a hiding place.

Do not assume that she remains visible at all times.

### Ghost hiding system

Potential hiding locations include:

- Cupboards.
- Drawers.
- Under the bed.
- Other appropriate hiding areas.
- Attic hiding spots.

The system should allow hiding locations to be configured without unnecessary hardcoded scene dependencies.

### Ghost AI architecture

The required AI behaviours are:

- Hide.
- Lure.
- Flee.

The exact internal implementation is not predetermined.

When we begin implementation, inspect existing scripts and propose an appropriate simple and optimized solution.

Do not automatically introduce a complicated behaviour tree or external AI framework.

---

# 8. Flashlight Capture System

The player captures the ghost using a flashlight.

Required behaviour:

1. The player aims at the ghost.
2. The system detects whether the ghost is inside the flashlight beam.
3. A capture timer runs while the ghost remains correctly illuminated.
4. After three continuous seconds, the capture succeeds.
5. The ghost disappears.
6. The capture counter increases.
7. The next round begins automatically.

If the player loses the ghost before the required three seconds, the ghost escapes and hides again.

The capture system must correctly consider:

- Beam direction.
- Beam angle.
- Distance.
- Visibility.
- Obstacles where appropriate.
- Continuous capture duration.

Keep the implementation efficient for standalone VR.

A visible capture timer is a Should feature.

A special capture animation is a Could feature.

---Section 8 — Flashlight Capture System

Interaction with Fake Escape

The flashlight remains the primary method of capturing the ghost.

The player must illuminate the ghost for three continuous seconds.

During Fake Escape, the player may have a short opportunity to aim the flashlight while the ghost watches them.

If the player fails to maintain the flashlight beam on the ghost for the required duration, the ghost escapes and hides again.

The ghost can also attempt to escape immediately when leaving a hiding location.

The flashlight capture system must work with both Fake Escape behaviours.

Interaction with the Holy Pistol

The player can use the Holy Pistol to affect the ghost's movement.

A successful Holy Pistol hit does not count as a capture.

The ghost must still be illuminated by the flashlight for three continuous seconds.

Holy Judgment must not bypass the flashlight capture requirement.

# 9. Six-Round Progression

The player must capture the same ghost six times.

### Rounds 1–3

The ghost appears in the Living Room and Bedroom.

These rounds gradually introduce and increase difficulty.

After each successful capture:

- The ghost can move faster.
- The ghost hides more intelligently.
- The ghost can lure the player more often.

### Attic unlock

After the third successful capture:

1. Loud banging is heard upstairs.
2. The Attic hatch opens.
3. The Attic becomes accessible.

### Round 4

The ghost moves to the Attic.

Round 4 takes place in the Attic.

### Rounds 5–6

The ghost may now appear in all three rooms:

- Living Room.
- Bedroom.
- Attic.

### Final capture

After the sixth successful capture, the player wins.

The Victory screen appears directly.

A special final action or multiple endings are only Could features.

---

# 10. Danger Meter

The Danger Meter represents the danger experienced by the player.

### Required behaviour

- The Danger Meter decreases when the ghost is nearby.
- The meter can automatically recover over time when the danger is reduced.
- If the meter becomes completely empty, the player loses immediately.
- The selected difficulty affects how the meter behaves.

Do not invent exact values, depletion rates, recovery times, or distances.

These values must be agreed upon and playtested.

The Danger Meter must connect correctly to:

- Ghost proximity.
- Difficulty settings.
- Game Over logic.
- User interface.

A heartbeat or static sound when the meter decreases is a Should feature.

---

Section 11 — Holy Pistol

Priority: Must

The Holy Pistol is a required supernatural gameplay item.

The player must find it somewhere inside the house.

Its exact location has not been determined.

The player can hold the Holy Pistol in one hand and the flashlight in the other hand at the same time.

Holy Pistol visual and audio effects

When fired, the Holy Pistol produces:

A supernatural holy sound.

A glowing cross visual effect.

Visual feedback showing that the pistol has been activated.

Holy Judgment — Must

Holy Judgment is the confirmed hit reaction system for the Holy Pistol.

The system has two different hit results.

Direct Hit — Temporary Stun

When the player lands a successful direct hit on the ghost:

A glowing cross effect appears.

The ghost is temporarily stopped.

The player receives an opportunity to aim the flashlight more accurately.

The player can attempt to capture the ghost while she is stopped.

The Holy Pistol itself does not capture the ghost.

Weak Hit — Forced Escape

When the player lands a weak hit:

The ghost reacts to the hit.

The ghost is driven away.

She escapes toward another hiding location.

The player must locate her again.

The ghost may move through walls while escaping.

Holy Judgment rules

Direct hits temporarily stop the ghost.

Weak hits drive the ghost away.

The flashlight is still required to capture the ghost.

The capture counter only increases after a successful flashlight capture.

The Holy Pistol must work with the ghost's hiding, fleeing, and Fake Escape behaviours.

Values requiring confirmation

The following technical details have not been finalized:

Exact direct-hit detection requirements.

Exact weak-hit detection requirements.

Stun duration.

Holy Pistol cooldown.

Ammunition or unlimited-use behaviour.

Exact visual effect duration.

Ask before choosing these values.

Do not introduce a conventional damage or health system for the ghost unless explicitly approved.

# 12. UV Flashlight and Ghost Traces

The flashlight is planned to support two functions:

- Normal flashlight mode.
- UV mode.

UV mode is connected to the Ghost Traces idea.

Ghost Traces can include:

- Footprints.
- Handprints.
- Other ghostly traces.

The player may use UV light to reveal or follow these traces.

**Important:** Ghost Traces is currently an assigned Should feature, not automatically a required core mechanic.

The normal flashlight capture system must work independently of Ghost Traces.

The flashlight system is a shared programming responsibility.

The Ghost Traces system is my personal responsibility.

---

# 13. Interactions and Physics

The player should be able to interact with objects using VR controllers.

Core interactions include:

- Grabbing objects.
- Holding objects.
- Releasing objects.
- Throwing objects.
- Opening doors.
- Opening drawers.
- Opening cupboards.
- Operating lamps.
- Using the flashlight.
- Storing items on the invisible belt.

At least five different kinds of objects should be physically interactable.

Examples from the GDD:

- Flashlight.
- Picture frame.
- Teddy bear.
- Diary.
- Toy.

Interaction systems must be comfortable and responsive in VR.

Do not duplicate another teammate's interaction framework.

---

# 14. Spatial Audio and Sound Effects

Spatial audio is essential to the game.

Players should locate the ghost by listening to the direction of sounds.

### Required sound categories

**Ghost sounds**

- Whispering.
- Giggling.
- Calling.
- Movement and footsteps.

**Environmental sounds**

- Creaking wooden floors.
- Wind.
- Old house noises.

**Interaction sounds**

- Opening doors.
- Opening cupboards.
- Opening drawers.
- Objects falling or colliding.

**Flashlight sounds**

- Turning the flashlight on or off.
- Capture progress.
- Successful capture.
- Ghost escaping.

**Attic event**

- Loud banging upstairs.
- Attic hatch opening.

**Holy Pistol**

- Supernatural holy firing sound.

Additional atmospheric sound effects may be added if appropriate.

Spatial audio is a shared programming responsibility.

Some audio assets may be made or selected collaboratively with the artists/designers.

Never claim sound assets exist before checking the project.

---

# 15. Haptic Feedback

The VR controllers should provide vibration feedback.

Required examples:

- Picking up an object.
- The ghost being nearby.

Haptics should support interactions and tension.

They should not become distracting or uncomfortable.

Controller haptics are primarily the other programmer's responsibility.

Do not modify that system without permission.

---

# 16. UI and HUD

The game should use clear, readable VR world-space UI.

The desired visual direction is inspired by bodycam-style interfaces.

However, VR comfort is more important than blindly copying a screen-space bodycam interface.

### Required UI

**Main Menu**

- Start.
- Settings.
- Quit.

**Chapter Selection**

- Chapter 1.

**Difficulty Selection**

- Easy.
- Normal.
- Hard.

**Room Selection**

- Living Room.
- Bedroom.
- Locked Attic.

**Pause Menu**

- Resume.
- Teleportation.
- Settings.
- Quit.

**Gameplay HUD**

- Danger Meter.
- Ghost Capture Counter.

**Victory Screen**

- Play Again.
- Main Menu.
- Quit.

**Lose Screen**

- Play Again.
- Main Menu.
- Quit.

### Additional UI

- Capture timer: Should.
- Save button: Could.

### Figma

Figma is the reference for our menu and interface designs.

Prefer reproducing approved Figma designs accurately.

Do not silently redesign existing interfaces.

### Mobbin

Mobbin can be used to find examples of clean and professional interfaces.

Use it for inspiration rather than direct copying.

### Unity implementation

Use suitable world-space VR UI.

Ensure that buttons and panels can be comfortably viewed and interacted with using VR controllers.

UI work is one of my personal responsibilities.

---

# 17. Pause System

The player can pause during gameplay.

Pausing stops gameplay activity.

The Pause Menu includes:

- Resume.
- Teleportation.
- Settings.
- Quit.

Resume returns to gameplay.

The teleportation option lets the player access the game's teleportation movement functionality.

A Save Game button is optional and should only be added if the team approves that extra feature.

Do not assume the game already contains a save system.

---

# 18. Win and Lose Conditions

## Winning

The player wins when the ghost has been successfully captured six times.

The Win Screen appears.

The player can:

- Play Again.
- Return to Main Menu.
- Quit.

## Losing

The player loses when the Danger Meter becomes empty.

The Lose Screen appears immediately.

The player can:

- Play Again.
- Return to Main Menu.
- Quit.

A Game Over animation is optional.

An additional ending or secret ending is also optional.

---

# 19. Complete Asset List

This section describes planned assets and requirements.

Asset names are design requirements, not verified Unity file paths.

## Environment assets

| Asset | Description | Priority |
|---|---|---|
| Living Room | Furnished old living room | Must |
| Bedroom | Furnished old girl's bedroom | Must |
| Attic | Furnished attic environment | Must |
| Wardrobe | Old vintage wardrobe | Must |
| Bookcase | Old wooden bookcase | Must |
| TV Cabinet | Old television cabinet | Must |
| Sofa | Worn leather three-seat sofa | Must |
| Bed | Old white wooden bed frame | Must |
| Coffee Table | Low wooden table | Must |
| Spiderwebs | Optimized decorative spiderweb planes/textures | Must |
| Carpets | Living room and bedroom carpets | Must |
| Toy Box | Container for children's toys | Should |
| Lamps | Standing lamp and hanging light bulb | Must |
| Pillows | Sofa cushions | Must |
| Bedding | Pink duvet and pillow | Must |
| Old Television | Old bulky television with antenna and side controls | Must |
| Nightstand | Small vintage girl's bedside table | Must |
| Curtains | Floral-patterned curtains with stains | Must |
| Damaged Family Pictures | Picture frames showing the same family | Should |
| Mirrors | Standing and wall mirrors | Should |
| Children's Toys | Wooden horse, dolls, blocks, toy tea set | Should |

## Character and equipment assets

| Asset | Description | Priority |
|---|---|---|
| Ghost Girl | Mid-poly girl with black hair and white nightgown | Must |
| Flashlight | Handheld flashlight | Must |
| Holy Pistol | Supernatural pistol with holy symbols | Must |
| VR Hands | Virtual player hands | Must |
| Basic Ghost Animations | Animations required for ghost behaviour | Must |
| Holy Pistol Effects | Magical cross and related effects | Must |

## UI assets

| Asset | Description | Priority |
|---|---|---|
| Main Menu UI | Start, Settings, Quit | Must |
| Chapter Selection UI | Chapter 1 selection | Must |
| Difficulty UI | Easy, Normal, Hard | Must |
| Room Selection UI | Living Room, Bedroom, locked Attic | Must |
| Pause Menu UI | Resume, Teleportation, Settings, Quit | Must |
| Danger Meter UI | Displays danger state | Must |
| Capture Counter UI | Displays captures out of six | Must |
| Win Screen | Victory interface | Must |
| Lose Screen | Defeat interface | Must |
| Capture Timer UI | Three-second illumination progress | Should |

## Sound assets

| Asset | Description | Priority |
|---|---|---|
| Ghost Voice | Whispering, giggling, calling | Must |
| House Ambience | Creaking and wind | Must |
| Attic Event | Banging and opening hatch | Must |
| Door and Drawer Sounds | Interactive environment audio | Must |
| Flashlight Sounds | On/off and capture sounds | Must |
| Capture and Escape Sounds | Success or ghost fleeing | Must |
| Object Sounds | Dropping and throwing objects | Must |
| Holy Pistol Sound | Supernatural holy firing sound | Must |
| Danger Meter Sound | Heartbeat or static | Should |

Some assets may be shared between systems.

Do not create duplicates unnecessarily.

Always check whether an asset has already been created by the artists before suggesting a replacement.

---

# 20. Additional Game Ideas

These are possible extensions, not automatically required gameplay.

## Should features

### Cursed Relics

Objects connected to the ghost's past.

Currently intended mainly for decoration and environmental storytelling.

### Rituals

Potential atmosphere and storytelling interactions.

Exact gameplay behaviour is not yet defined.

### Ghost Traces

Footprints or handprints revealed by the UV flashlight.

### Ghost Phone

An old telephone that occasionally rings.

The player may hear the ghost whispering, laughing, or providing information.

### Hint System

Give the player a hint after approximately 60 seconds without finding the ghost.

## Could features

### Haunted Mirror

A mirror that can sometimes show the ghost or an unusual reflection.

### Music Box

A music box that could attract the ghost.

### Frozen Clock

A clock that stops ticking when the ghost is nearby.

### Optional Introduction

A short story introduction before gameplay.

### Optional Animations

Additional ghost capture or Game Over animations.

### Extra Tasks

Optional exploration tasks, such as finding a diary.

### Extra Endings

Additional endings if the team has enough time.

### Additional Attic Interaction

Pulling a rope to open the attic stairs.

Do not implement these systems unless they are explicitly selected for the current development task.

---

# 21. User Stories

These user stories describe the intended player experience.

## Player

- As a player, I want to explore the house so I can discover its rooms.
- As a player, I want to search for the ghost so I can capture her.
- As a player, I want to pick up a flashlight so I can see in the dark.
- As a player, I want to aim the flashlight so I can capture the ghost.
- As a player, I want to hear sounds from different directions so I can find the ghost.
- As a player, I want to illuminate the ghost for three seconds so I can capture her.
- As a player, I want to search again when the ghost escapes so the game remains challenging.

## Ghost

- As a player, I want the ghost to make sounds so I can locate her.
- As a player, I want the ghost to hide so I must search carefully.
- As a player, I want the ghost to use different hiding spots so searching stays interesting.
- As a player, I want the ghost to approach me so the tension increases.
- As a player, I want to recognize different ghost sounds so I can follow audio clues.
- As a player, I want the ghost to disappear after capture so I know I succeeded.

## Flashlight

- As a player, I want to hold the flashlight with VR hands so it feels natural.
- As a player, I want to rotate and aim the flashlight so I can illuminate different areas.
- As a player, I want the ghost to react to the flashlight so I can capture her.
- As a player, I want to see the flashlight beam clearly so I can aim accurately.
- As a player, I want to store and retrieve the flashlight using my invisible belt so it remains accessible.

## Spatial Audio

- As a player, I want directional sound so I can determine the ghost's location.
- As a player, I want nearby sounds to be easier to hear so I can recognize when the ghost is close.
- As a player, I want atmospheric house sounds so the environment feels haunted.
- As a player, I want to hear ghost sounds through the environment so I can search without seeing her.
- As a player, I want sounds that match the environment so the house feels believable.

## Rooms and Environment

- As a player, I want three different rooms so I can explore new locations.
- As a player, I want to open doors so I can move through the house.
- As a player, I want to open drawers and cupboards so I can investigate hiding spots.
- As a player, I want the game to become more difficult as I progress.
- As a player, I want a dark haunted environment so I can experience tension.

## Progression

- As a player, I want to capture the ghost six times so I have a clear goal.
- As a player, I want to see the number of successful captures so I know my progress.
- As a player, I want three difficulty settings so I can choose the challenge.
- As a player, I want the ghost to hide again if she escapes so the search continues.
- As a player, I want feedback after a capture so I know it succeeded.

## VR Controls

- As a player, I want to pick up objects with VR controllers so interactions feel natural.
- As a player, I want to open doors and drawers using my virtual hands.
- As a player, I want comfortable movement so I can explore the house.
- As a player, I want controller vibration when the ghost is nearby.
- As a player, I want straightforward flashlight controls so I can focus on searching.

## Interface

- As a player, I want a capture counter so I know how many captures remain.
- As a player, I want feedback when I capture the ghost.
- As a player, I want clear instructions so I understand the game objective.
- As a player, I want minimal visual distractions so I can focus on the environment.

## Atmosphere

- As a player, I want to explore a dark old house so I experience suspense.
- As a player, I want to recognize the ghost against the environment.
- As a player, I want atmospheric lighting that supports the horror style.
- As a player, I want suspense without jumpscares.
- As a player, I want an environment that feels believable while remaining optimized for VR.

## Comfort

- As a player, I want comfortable movement so I do not become unnecessarily motion sick.
- As a player, I want to control my own head and hand movement.
- As a player, I want to avoid unexpected camera movement.
- As a player, I want the game's tension to come from sound and atmosphere rather than sudden jumpscares.

---

# 22. Team Development Responsibilities

## My tasks

I work primarily on:

- Quest build and testing.
- Danger Meter UI.
- Capture Counter UI.
- Win/Lose screens.
- Ghost Traces.
- Holy Pistol.
- Start Menu.
- Difficulty settings.
- Danger Meter logic.
- Win/Lose game logic.
- Ghost hiding spots.
- Attic unlock after capture three.
- Six-round progression.
- Ghost AI.
- Integration between gameplay systems.

## Other programmer's tasks

The other programmer primarily handles:

- VR base setup.
- VR hands.
- Movement.
- Teleportation.
- VR comfort.
- Object grabbing.
- Object throwing.
- Invisible belt.
- Controller haptics.
- Interactive doors, drawers, cupboards, and lamps.

## Shared programming tasks

- Flashlight system.
- Normal and UV light modes.
- Spatial audio.

## Artists/designers

The two artists/designers create:

- Ghost model.
- Old house environment.
- Furniture.
- Props.
- Visual assets.
- Some audio and sound effects.

I am responsible for connecting the gameplay systems together.

Always respect team ownership and ask permission before modifying another person's work.

---

# 23. Development Order

Use this as a recommended order, not as proof that tasks are unfinished.

First, inspect the Unity project and determine what is already implemented.

Then prioritize the core dependencies.

### Foundation

- Verify Unity and XR setup.
- Verify VR hands.
- Verify movement.
- Verify interaction system.
- Verify flashlight grabbing.
- Verify invisible belt.

### First playable gameplay

- One test room.
- One functional ghost.
- Spatial audio.
- Hiding and searching.
- Flashlight detection.
- Three-second capture timer.

### Complete game loop

- Six captures.
- Increasing difficulty.
- Ghost AI.
- Danger Meter.
- Win/Lose conditions.
- Attic unlock.
- Three-room integration.

### UI and game experience

- Start Menu.
- Chapter Selection.
- Difficulty Selection.
- Room Selection.
- Gameplay HUD.
- Pause Menu.
- Win/Lose screens.

### Additional confirmed feature

- Holy Pistol.

### Improvements and optional content

- Ghost Traces.
- Other approved Should features.
- Could features only when time permits.

Testing and optimization must happen throughout development, not only at the end.

Do not assume the suggested order overrides a specific task assignment or deadline.

---

# 24. Technical and Design Priorities

Whenever you help me, prioritize the following:

1. Correctness.
2. Respecting existing project systems.
3. Respecting team responsibilities.
4. Performance on Meta Quest 3.
5. VR comfort.
6. Code maintainability.
7. Visual quality within the performance budget.
8. Clear beginner-friendly implementation instructions.
9. Accurate integration between systems.
10. Testing and debugging.

Avoid unnecessary complexity.

Prefer small, reliable implementations over large, untested systems.

Use Figma for approved UI designs and Mobbin for interface inspiration.

Use CoplayDev MCP to inspect the actual Unity project when appropriate.

Do not invent components, assets, packages, or scene structures.

Do not take control away from me.

---

Section 25 — Updated Information That Still Needs Confirmation

The following gameplay decisions are confirmed:

Fake Escape is a Must feature.

The ghost sometimes stops and looks at the player.

The ghost can also escape immediately.

The ghost can move through walls.

Holy Judgment is a Must feature.

Direct Holy Pistol hits temporarily stop the ghost.

Weak Holy Pistol hits drive the ghost away.

The Holy Pistol does not replace flashlight capture.

The player can use the flashlight and Holy Pistol simultaneously.

The following details still need confirmation:

Probability of each Fake Escape behaviour.

Duration of the ghost's brief pause.

Exact direct-hit and weak-hit detection.

Holy Pistol stun duration.

Holy Pistol cooldown.

Holy Pistol ammunition behaviour.

Exact behaviour when the ghost is temporarily stopped during a capture attempt.

Do not invent these technical values.

Present reasonable options and request approval before implementing them.

All other requirements, gameplay mechanics, MoSCoW priorities, team responsibilities, and technical rules remain unchanged.

# 26. Instructions to Gemini When Reading This Document

When I ask you to help develop a feature:

1. Read the relevant sections of this document.
2. Follow the working rules in `GEMINI.md`.
3. Verify the actual project state before proposing code.
4. Check whether the feature is my responsibility, shared, or owned by another team member.
5. Check whether the feature is Must, Should, or Could.
6. Explain a suitable implementation for Unity 6.3 LTS.
7. Present a short plan.
8. Provide clear manual Unity Editor steps.
9. Provide understandable English C# code.
10. Ask before modifying any project files, scenes, prefabs, or settings.
11. Help me test the implementation.
12. Explain the results and remaining work.

Do not assume everything described in this document has already been created.

Your goal is to help me build **Kamer vol spoken** correctly, efficiently, and professionally while allowing me to learn Unity and stay in control of development.