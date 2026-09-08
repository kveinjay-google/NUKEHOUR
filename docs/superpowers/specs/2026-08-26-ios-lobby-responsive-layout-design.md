# iOS Lobby and Multiplayer Responsive Layout Design

## Goal

Fix the deterministic iOS layout failures in the skirmish lobby, server-creation screen, and multiplayer browser in one coherent pass. The map image, title, metadata, validation/download state, progress bar, update/install/retry actions, selected-server details, dynamic client list, and footer controls must remain readable, contained, and non-overlapping on supported iPhone and iPad landscape screens.

The supported verification matrix is:

- iPhone 844 × 390 points rendered at 1558 × 720 logical pixels, including safe-area insets.
- iPhone 932 × 430 points rendered at 1560 × 720 logical pixels, including safe-area insets.
- iPad mini 1024 × 768 points.
- iPad mini/current compact tablet snapshot 1133 × 744 points.
- iPad Air 1180 × 820 points.
- iPad Pro 1366 × 1024 points.

## Root cause

OpenRA resolves `PARENT_WIDTH` and `PARENT_HEIGHT` while loading a widget. Changing a parent `Bounds` later does not recompute its descendants. The current iOS code resizes only the outer map and selected-server containers, while nested widgets retain desktop coordinates such as a 172 × 172 map, metadata at `Y=188/201`, and progress/retry controls at `Y=194`.

Two duplicate map-layout methods also assign the full map-card rectangle to every direct child before special-casing only `MAP_LARGE` and `MAP_SMALL`. Direct buttons such as `MAP_UPDATE` and `MAP_RETRY` therefore become full-card overlays, while the text and progress widgets inside state containers remain at their stale desktop positions.

The selected-server layout reserves four text rows but places five. `ServerListLogic` then caches the original desktop join-button `Y=255` before iOS layout runs and restores that value after server selection. The nested selected-map preview and dynamically loaded client list are never resized.

Finally, the compact lobby gives the embedded server browser only the short player-list region. On the 844-point iPhone this leaves no usable server-list height. A child-only fix cannot make that surface fit.

## Architecture

### Shared map-preview layout

Add one pure `IosMapPreviewLayout` policy and one widget-tree application seam. Both `LobbyLogic` and `IosTouchMenuLogic` must call this shared implementation; the two duplicate resize methods are removed.

The policy exposes distinct geometry for the large playable preview and the small status preview:

- map surface and nested one-pixel-inset `MapPreview`;
- title;
- primary and secondary metadata/status lines;
- progress bar;
- primary and secondary update/install/retry actions.

Every state container receives the full card bounds, but direct labels and buttons receive only their semantic slots. The layout covers `MAP_AVAILABLE`, `MAP_INCOMPATIBLE`, `MAP_VALIDATING`, `MAP_DOWNLOAD_AVAILABLE`, `MAP_UPDATE`, `MAP_UPDATE_DOWNLOAD_AVAILABLE`, `MAP_SEARCHING`, `MAP_UNAVAILABLE`, `MAP_ERROR`, `MAP_DOWNLOADING`, `MAP_RETRY`, and `MAP_UPDATE_AVAILABLE`.

On compact phones, the map surface is on the left and the title/status/action stack is on the right. The control column is at least two minimum touch targets wide, and this minimum is applied before choosing the map square, so translated action text has room and action buttons remain at least 48 physical points. On iPads, the map is above the title/status/action rows. Large and small preview variants reserve different lower bands so a progress bar or action never overlaps metadata.

`UpdateDownloadAvailable` is a special two-action state: `MAP_INSTALL` and `MAP_UPDATE` are simultaneously visible and receive distinct stacked 48-point slots. Other states may reuse either slot only when the competing widget is not visible.

The map and metadata labels use the existing iOS fonts. Short metadata remains single-line and is width-aware truncated. Potentially long searching, validating, downloading, unavailable, update, and error messages receive a true two-line-height status slot before wrapping is enabled; `LabelWidget` is not relied upon to clip overflow. No new assets or translations are required.

`MapPreviewLogic` currently caches truncation using the desktop font and width before the iOS layout runs. Its title/type/author caches must include current bounds width and current font in their key. This makes the same map recalculate after rotation, safe-area changes, and compact/tablet transitions.

### Lobby surface allocation

Add an explicit full `Main` region to `IosLobbyLayout`.

For normal player/options/music tabs:

- iPad retains the existing player/map columns and the chat row.
- iPhone lets the map card use the full main-region height. The change-map action moves to the center cell of the footer, between Start and Disconnect, instead of consuming most of the map-card height. The player list and chat remain in the left column.

For the Servers tab on every iOS device:

- the map and map action are already hidden by existing lobby behavior;
- the chat surface is hidden while this tab is active;
- `LOBBY_SERVERS_BIN` receives the full `Main` region rather than the short player-list region;
- all other tab panels retain the normal player-region bounds.

This makes the server tab a dedicated responsive surface without adding a new product-visible tab or changing desktop behavior.

### Multiplayer browser and details

Extend `IosMultiplayerBrowserLayout` to include `PLAYER_COUNT` in the footer. The footer contains six non-overlapping cells: Filters, Reload, Player Count, Direct Connect, Create, and Back. Server-row password/authentication icons are anchored inside the title column, and the title width reserves their space.

Add a state-aware pure `IosMultiplayerDetailsLayout` and widget-tree application seam. It accepts whether a client list and Join action are present, and lays out:

- selected map background and nested `SELECTED_MAP_PREVIEW`;
- selected map name, address, status, mod version, and player summary;
- dynamic `CLIENT_LIST_CONTAINER` and its loaded scroll panel;
- Join button.

On compact phones, the map is a bounded square at upper left, while the right-hand text column is reserved at a minimum of two touch-target widths before the map size is chosen. Five summary rows use that column when there are no clients. When clients are present, the already-hidden `SELECTED_PLAYERS` row is reclaimed for the client list. The resulting list must contain at least one 24-point header and one complete 48-point player row. Join is fixed to the bottom. On iPads, the map is above the summary rows, followed by the client list and bottom Join action, with the same player-row reclamation rule.

`ServerListLogic` must not restore the desktop join-button coordinate on iOS. Dynamic client headers and player rows are adapted after cloning so their fonts, heights, flag, and labels use the current iOS geometry. Selected-map and mod-version truncation caches include current width and font, so selecting the same server after a layout change recomputes the text. Desktop keeps its existing coordinate behavior.

For the embedded lobby server browser, the filter/reload footer is reserved under the server table while the details column may use the full panel height.

## Behavioral invariants

- Every visible widget has positive width and height and is contained by its immediate layout surface.
- Widgets that share a slot across mutually exclusive states may overlap in storage; widgets visible in the same state may not overlap.
- Nested map previews fill their resized backgrounds with a one-pixel inset.
- Progress bars never occupy a touch-action slot and never overlap status text.
- The simultaneous Install and Update actions have separate 48-point slots.
- Update, install, retry, change-map, Join, and footer buttons remain at least 48 physical points in both dimensions.
- The map surface remains at least 48 physical points in both dimensions.
- Five selected-server summary rows are reserved without clients; the hidden player-summary row is explicitly reclaimed when clients are shown, and client-list and Join regions never overlap visible rows.
- A visible client list can show at least one complete header and one complete player row.
- Selecting servers with and without client lists cannot move Join back to desktop `Y=255`.
- Reapplying layout after resize, rotation, dynamic row creation, or state changes is deterministic and idempotent.
- Desktop behavior and geometry are unchanged.

## Testing strategy

Add executable pure-layout and widget-tree tests for both iPhones, both iPad mini snapshots (1024 × 768 and the current 1133 × 744 effective snapshot), iPad Air, and iPad Pro.

Map tests exercise playable, validating/downloading, update/install, the simultaneous update-plus-install state, and retry/error presentations. They assert positive geometry, immediate-parent containment, semantic non-overlap, nested preview fill, two distinct 48-point action slots, measured/wrapped text containment, 48-point map surface, and idempotent reapplication. A same-map rotation test proves truncation is recalculated for the new width/font.

Multiplayer tests exercise no selection, summary-only selection, and client-list selection, including Summary → Clients → Summary transitions. They assert visible summary rows, reclamation of the hidden player row, at least one complete client header/player row, nested map preview, dynamic client list, fixed Join placement, footer player count, server icons, width/font-aware retruncation, and no return to desktop coordinates.

The compact lobby tests additionally prove that the normal map card has usable height and the Servers tab receives the full main surface. The matrix uses real effective/native sizes so iPhone logical scaling cannot be accidentally tested as 1:1.

## Delivery boundary

This change modifies only iOS responsive policies, their runtime wiring, `MapPreviewLogic`'s width/font-aware text caches, `ServerListLogic`'s iOS-specific selection/dynamic-row/join behavior, and focused tests. It does not redesign desktop chrome, add art, change networking, or modify server discovery. Because the engine tree is materialized from a pinned upstream release, all engine changes must also be captured in the next replayable engine patch and verified from a fresh official source archive.
