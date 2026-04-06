# Commands

This folder contains Revit command entry points.

Each command should stay thin and focus on composition:
- create dependencies
- create the ViewModel
- trigger execution through the base command

Business logic should not be implemented directly in command classes.
