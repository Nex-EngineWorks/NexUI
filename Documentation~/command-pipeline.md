# Command pipeline

Designer command keys are resolved at runtime through `UIActionResolver` and command binders.
They are not executable merely because a key was entered in metadata.

Use a single registry of project command keys, validate missing registrations, and keep command
handlers free of backend-specific UI types. Commands implementing `IUndoableCommand` can
participate in replay and inversion workflows.
