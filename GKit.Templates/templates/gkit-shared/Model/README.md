# Model

Entities live here. `gkit add crud --entity Vehicle` writes `Vehicle.cs` into this folder, its
validator into `Validation/`, its query factory into `Data/`, and the grid, form, dialog and page
into the host project - because those are the only parts that depend on the UI adapter.
