
## ingame-debug.yaml
label-debug-panel-title = Debug Options
checkbox-debug-panel-instant-build = Instant Build Speed
checkbox-debug-panel-enable-tech = Build Everything
checkbox-debug-panel-build-anywhere = Build Anywhere
checkbox-debug-panel-unlimited-power = Unlimited Power
checkbox-debug-panel-instant-charge = Instant Charge Time
checkbox-debug-panel-disable-visibility-checks = Disable Visibility Checks
button-debug-panel-give-cash = Give $20,000
button-debug-panel-grow-resources = Grow Resources
button-debug-panel-give-exploration = Clear Shroud
button-debug-panel-reset-exploration = Reset Shroud
label-debug-panel-visualizations-title = Visualizations
checkbox-debug-panel-show-unit-paths = Show Unit Paths
checkbox-debug-panel-show-customterrain-overlay = Show Custom Terrain
checkbox-debug-panel-show-actor-tags = Show Actor Tags
checkbox-debug-panel-show-depth-preview = Show Depth Data
checkbox-debug-panel-show-combatoverlay = Show Combat Geometry
checkbox-debug-panel-show-geometry = Show Render Geometry
checkbox-debug-panel-show-terrain-overlay = Show Terrain Geometry
checkbox-debug-panel-show-screenmap = Show Screen Map

## ingame-observer.yaml
button-observer-widgets-options = Options (Esc)
button-replay-player-pause-tooltip = Pause
button-replay-player-play-tooltip = Play

button-replay-player-slow =
    .tooltip = Slow speed
    .label = 50%

button-replay-player-regular =
    .tooltip = Regular speed
    .label = 100%

button-replay-player-fast =
    .tooltip = Fast speed
    .label = 200%

button-replay-player-maximum =
    .tooltip = Maximum speed
    .label = MAX

label-basic-stats-player-header = Player
label-basic-stats-cash-header = Cash
label-basic-stats-power-header = Power
label-basic-stats-kills-header = Kills
label-basic-stats-deaths-header = Deaths
label-basic-stats-assets-destroyed-header = Destroyed
label-basic-stats-assets-lost-header = Lost
label-basic-stats-experience-header = Score
label-basic-stats-actions-min-header = APM
label-economy-stats-player-header = Player
label-economy-stats-cash-header = Cash
label-economy-stats-income-header = Income
label-economy-stats-assets-header = Assets
label-economy-stats-earned-header = Earned
label-economy-stats-spent-header = Spent
label-economy-stats-harvesters-header = Harvesters
label-production-stats-player-header = Player
label-production-stats-header = Production
label-support-powers-player-header = Player
label-support-powers-header = Support Powers
label-army-player-header = Player
label-army-header = Army
label-combat-stats-player-header = Player
label-combat-stats-assets-destroyed-header = Destroyed
label-combat-stats-assets-lost-header = Lost
label-combat-stats-units-killed-header = U. Killed
label-combat-stats-units-dead-header = U. Lost
label-combat-stats-buildings-killed-header = B. Killed
label-combat-stats-buildings-dead-header = B. Lost
label-combat-stats-army-value-header = Army Value
label-combat-stats-vision-header = Vision

## ingame-player.yaml
supportpowers-support-powers-palette =
    .ready = READY
    .hold = ON HOLD

button-command-bar-production-x5 =
    .short = ×5
    .tooltip = Produce Five
    .tooltipdesc = Toggle five-at-a-time production. While enabled, each tap queues five units or standard buildings. Unique structures still obey their build limit.

button-command-bar-attack-move =
    .label = Atk Move
    .tooltip = Attack Move
    .tooltipdesc = Selected units will move to the desired location
    and attack any enemies they encounter en route.
    
    Hold <(Ctrl)> while targeting to order an Assault Move
    that attacks any units or structures encountered en route.
    
    Left-click icon then right-click on target location.

button-command-bar-force-move =
    .label = Force
    .tooltip = Force Move
    .tooltipdesc = Selected units will move to the desired location
     - Default activity for the target is suppressed
     - Vehicles will attempt to crush enemies at the target location
    
    Left-click icon then right-click on target.
    Hold <(Alt)> to activate temporarily while commanding units.

button-command-bar-force-attack =
    .label = Force Atk
    .tooltip = Force Attack
    .tooltipdesc = Selected units will attack the targeted unit or location
    ignoring their default activity for the target.
    
    Left-click icon then right-click on target.
    Hold <(Ctrl)> to activate temporarily while commanding units.

button-command-bar-guard =
    .label = Guard
    .tooltip = Guard
    .tooltipdesc = Selected units will follow the targeted unit.
    
    Left-click icon then right-click on target unit.

button-command-bar-deploy =
    .label = Deploy
    .tooltip = Deploy
    .tooltipdesc = Selected units will perform their default deploy activity
     - MCVs will unpack into a Construction Yard
     - Construction Yards will re-pack into a MCV
     - Transports will unload their passengers
     - Demolition Trucks and MAD Tanks will self-destruct
     - Aircraft will return to base
    
    Acts immediately on selected units.

button-command-bar-scatter =
    .label = Scatter
    .tooltip = Scatter
    .tooltipdesc = Selected units will stop their current activity
    and move to a nearby location.
    
    Acts immediately on selected units.

button-command-bar-stop =
    .label = Stop
    .tooltip = Stop
    .tooltipdesc = Selected units will stop their current activity.
    Selected buildings will reset their rally point.
    
    Acts immediately on selected targets.

button-ios-viewport-action-stop =
    .label = Stop
button-ios-viewport-action-deploy =
    .label = Deploy
button-ios-viewport-action-select-type =
    .label = Type
button-ios-viewport-action-force-attack =
    .label = Force
button-ios-viewport-action-return-base =
    .label = Base

button-command-bar-queue-orders =
    .label = Waypoint
    .tooltip = Waypoint Mode
    .tooltipdesc = Use Waypoint Mode to give multiple linking commands
    to the selected units. Units will execute the commands
    immediately upon receiving them.
    
    Left-click icon then give commands in the game world.
    Hold <(Shift)> to activate temporarily while commanding units.

button-stance-bar-attackanything =
    .label = Attack
    .tooltip = Attack Anything Stance
    .tooltipdesc = Set the selected units to Attack Anything stance:
     - Units will attack enemy units and structures on sight
     - Units will pursue attackers across the battlefield

button-stance-bar-defend =
    .label = Defend
    .tooltip = Defend Stance
    .tooltipdesc = Set the selected units to Defend stance:
     - Units will attack enemy units on sight
     - Units will not move or pursue enemies

button-stance-bar-returnfire =
    .label = Return
    .tooltip = Return Fire Stance
    .tooltipdesc = Set the selected units to Return Fire stance:
     - Units will retaliate against enemies that attack them
     - Units will not move or pursue enemies

button-stance-bar-holdfire =
    .label = Hold
    .tooltip = Hold Fire Stance
    .tooltipdesc = Set the selected units to Hold Fire stance:
     - Units will not fire upon enemies
     - Units will not move or pursue enemies

button-top-buttons-fps-tooltip = Toggle FPS Display
button-top-buttons-debug-tooltip = Debug Menu
button-top-buttons-floating-controls-tooltip = Show/Hide Touch Controls
button-top-buttons-options-tooltip = Options
button-top-buttons-repair-tooltip = Repair (click buildings)
button-top-buttons-auto-repair-tooltip = Auto-Repair
button-top-buttons-power-tooltip = Auto-Repair
button-top-buttons-beacon-tooltip = Place Beacon
button-top-buttons-sell-tooltip = Sell
labelwithtooltip-player-widgets-cash = <0>
labelwithtooltip-player-widgets-power = <0>

productionpalette-sidebar-production-palette =
    .ready = READY
    .hold = ON HOLD

button-production-types-building-tooltip = Buildings
button-production-types-support-tooltip = Support
button-production-types-infantry-tooltip = Infantry
button-production-types-vehicle-tooltip = Vehicles
button-production-types-aircraft-tooltip = Aircraft
button-production-types-naval-tooltip = Naval
button-production-types-scroll-up-tooltip = Scroll up
button-production-types-scroll-down-tooltip = Scroll down
dropdownbutton-hpf-overlay-locomotor = Select Locomotor
dropdownbutton-hpf-overlay-check = Select BlockedByActor

## mainmenu-prerelease-notification.yaml
label-mainmenu-prerelease-notification-prompt-title = NUKE HOUR developer preview
label-mainmenu-prerelease-notification-prompt-text-a = This pre-alpha build of NUKE HOUR is made available
label-mainmenu-prerelease-notification-prompt-text-b = for the community to follow development and as example for modders.
label-mainmenu-prerelease-notification-prompt-text-c = Many features are missing or incomplete, performance has not been
label-mainmenu-prerelease-notification-prompt-text-d = optimized, and balance will not be addressed until a future beta.
button-mainmenu-prerelease-notification-continue = I Understand

## mainmenu.yaml
label-menu-subtitle = NUKE HOUR


## settings.yaml tabs
button-settings-title = GAME SETTINGS
button-settings-tab-display = Display
button-settings-tab-audio = Audio
button-settings-tab-input = Input
button-settings-tab-touch = Touch Controls
button-settings-tab-hotkeys = Hotkeys
button-settings-tab-advanced = Advanced
## settings-advanced.yaml
label-forum-account-section-header = Forum Account

## ingame-player.yaml selection bar
button-selection-bar-group-01 =
    .label = 1
button-selection-bar-group-02 =
    .label = 2
button-selection-bar-group-03 =
    .label = 3
button-selection-bar-group-04 =
    .label = 4
button-selection-bar-group-05 =
    .label = 5
button-selection-bar-group-06 =
    .label = 6
button-selection-bar-group-07 =
    .label = 7
button-selection-bar-group-08 =
    .label = 8
button-selection-bar-group-09 =
    .label = 9
button-selection-bar-group-10 =
    .label = 0
button-selection-bar-group =
    .tooltip = Control Group
    .tooltipdesc = Left-click to select this group.
    Double-click or Alt+click to jump the camera to the group.

    Ctrl+click to assign the current selection to this group.
    Shift+click to add this group to the current selection.
    Ctrl+Shift+click to add the selection into this group.
button-selection-bar-select-all =
    .label = Select All
    .tooltip = Select All Combat Units
    .tooltipdesc = First click selects combat units on screen.
    Click again to expand the selection across the whole map.
button-selection-bar-select-by-type =
    .label = Same Type
    .tooltip = Select Same Type
    .tooltipdesc = Expand the selection to other units of the same type.
    First click covers the screen, second click covers the whole map.

button-selection-bar-cycle-base =
    .label = Base
    .tooltip = Jump to Base
    .tooltipdesc = Cycle through and center on your base buildings.
button-selection-bar-to-selection =
    .label = Focus
    .tooltip = Jump to Selection
    .tooltipdesc = Center the viewport on the current selection.
button-selection-bar-to-last-event =
    .label = Event
    .tooltip = Jump to Last Radar Event
    .tooltipdesc = Center the viewport on the most recent radar ping.
button-selection-bar-cycle-harvesters =
    .label = Miner
    .tooltip = Cycle Harvesters
    .tooltipdesc = Cycle through and center on your harvesters.
button-selection-bar-remove-from-group =
    .label = Ungroup
    .tooltip = Remove from Control Group
    .tooltipdesc = Remove the selected units from their control group.
button-selection-bar-sell =
    .label = Sell
    .tooltip = Sell Mode
    .tooltipdesc = Enter sell mode and left-click a building to sell it.
button-selection-bar-repair =
    .label = Repair
    .tooltip = Repair Mode
    .tooltipdesc = Enter repair mode and left-click a building to repair it.
button-selection-bar-beacon =
    .label = Beacon
    .tooltip = Place Beacon
    .tooltipdesc = Place a beacon marker on the map.

button-command-bar-edit =
    .label = Edit
    .tooltip = Edit Command Bar
    .tooltipdesc = Enter edit mode: drag to reorder, click or right-click to show/hide commands.
    Click again to save and exit.
button-command-bar-collapse =
    .label = ▾
    .tooltip = Collapse Command Bar
    .tooltipdesc = Collapse to a compact classic-style strip.
button-command-bar-expand =
    .label = ▴
    .tooltip = Expand Command Bar
    .tooltipdesc = Expand to the full command bar with labels.
label-command-bar-edit-hint = Drag to reorder · Click to toggle · Right-click also toggles
