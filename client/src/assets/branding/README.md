# Pick & Roll — 07B Hoop Badge

The approved 07B design is the shared identity for the portal and Flutter application.

- Editable master: `pick-and-roll-hoop-badge.svg` (transparent, 256-unit viewBox).
- Raster master: `pick-and-roll-hoop-badge-master.png` (transparent, 1024 × 1024).
- Store/share icon: `hoop-badge-app-icon-1024.png` (opaque navy, 1024 × 1024).
- Browser icons have a navy background so the white letterform remains visible on light browser tabs.

Colors: navy `#07131F`, off-white `#F2F6FA`, orange `#FF8A32`.

## Exporting sizes

After changing the SVG, render it to the same-size transparent raster master (1024 × 1024, preserving the viewBox and padding). From `client`, run:

```powershell
.\scripts\export-branding.ps1 -MobileRoot "C:\Users\Nemanja\Documents\PickAndRoll"
```

The Windows PowerShell exporter uses System.Drawing without a package dependency. It produces portal favicons and touch icons, copies the shared source into the Flutter repo, and exports Android adaptive and legacy icons, native splash images, Apple icon catalogs, Flutter web icons, and a Windows ICO. All outputs are committed assets; normal Docker and Flutter builds do not need the export script or the other repository.

Android adaptive foregrounds include safe padding for circular masks. Apple app icons use opaque PNGs. Do not add text or a pre-rounded outer tile to store icons; the operating system supplies its own mask.

## Design provenance

The concept was explored with the built-in image-generation tool using this brief: “07B / HOOP BADGE: a compact rounded-square badge with an off-white P/R letterform and an orange perspective hoop; navy, orange and off-white; minimal geometry; exact wordmark PICK & ROLL.” The approved mark was rebuilt as an editable SVG for clean transparency and consistent exports. The automatic raster cutout with stray pixels is not used.
