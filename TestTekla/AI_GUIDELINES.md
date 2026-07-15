# AI Development Guidelines & Project Context

This document contains essential instructions and rules for AI agents modifying this codebase. **Read this document carefully before executing any changes.**

---

## 1. Universal Rules

### 🇬🇧 English-Only User Interface (Critical)
* **Rule**: This project is built for international clients/foreign users.
* **Instruction**: All user-facing strings, including:
  * `MessageBox` messages and titles
  * Picker pick prompts (e.g., `picker.PickPoint("...")`)
  * UI labels, tooltips, buttons, and status bar text
  * Error messages and validation alerts
  **MUST be written entirely in English**. Do not use Vietnamese text for any user-facing notifications.

### 🧪 Tekla Structures 2020 Integration
* **API Target**: Tekla Structures 2020.0 (`net48` Target Framework).
* **Connection Check**: Always verify connection status using `Model.GetConnectionStatus()` before interacting with the model.
* **Commit Changes**: Always invoke `Model.CommitChanges()` after creating, modifying, or deleting model objects to apply changes.
* **Selection / Picking**:
  * Wrap `Picker` actions (e.g., `PickPoint`, `PickObject`) in `try-catch` blocks to gracefully handle cases where the user presses `Esc` or cancels the picking command.
  * Use `ModelObjectSelector` to retrieve selected objects from the Tekla interface.

---

## 2. Project Architecture

### WPF & MVVM Pattern
* The project is a WPF desktop application targeting **.NET Framework 4.8**.
* ViewModels inherit from `BaseViewModel` (which implements `INotifyPropertyChanged`).
* Views are located under `Views/` and `Views/Pages/`.
* Code-behind files (e.g., `*.xaml.cs`) bind button click events to event handlers, which retrieve settings/values and call ViewModel execution methods.

---

## 3. Core Components

* **[CreateRebarViewModel.cs](file:///e:/0.CODE/TeklaTest/TestTekla/ViewModels/PageModels/CreateRebarViewModel.cs)**: Handles rebar creation, cloning, splitting, and point/node manipulation (adding, deleting, reversing points).
* **[PPVCAutoDimTagViewModel.cs](file:///e:/0.CODE/TeklaTest/TestTekla/ViewModels/PageModels/PPVCAutoDimTagViewModel.cs)**: Logic for PPVC automatic dimensioning and tagging of reinforcement bars.
* **[RebarInspectorViewModel.cs](file:///e:/0.CODE/TeklaTest/TestTekla/ViewModels/PageModels/RebarInspectorViewModel.cs)**: Manages properties, grouping, sorting, and numbering of reinforcement bars.
* **[RebarMethol.cs](file:///e:/0.CODE/TeklaTest/TestTekla/Models/RebarMethol.cs)**: Static helper methods for rebar copying/manipulation.
