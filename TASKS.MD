# Caret Tracker Service - Development Tasks

> **Note:** All comments and documentation in this project must be in English.

## Setup Tasks

- [] **TASK-001**: Initialize project structure
  - Create .NET 6.0 application
  - Setup solution and project files
  - Configure build settings for x64 architecture

- [ ] **TASK-002**: Create configuration system
  - Implement reading from `caret_config.json`
  - Support configurable update interval and output path
  - Add default configuration values

## Core Implementation Tasks

- [ ] **TASK-003**: Implement caret position detection
  - Use Windows API (`GetGUIThreadInfo`)
  - Implement coordinate conversion (`ClientToScreen`)
  - Handle edge cases (no focused window, no caret)

- [ ] **TASK-004**: Create JSON output system
  - Implement serialization using `System.Text.Json`
  - Follow required schema with `caret_` prefixes
  - Create output directory if it doesn't exist

- [ ] **TASK-005**: Implement timer for periodic tracking
  - Use `System.Threading.Timer` for tracking
  - Implement update frequency control
  - Add optimization to update only on position changes

## System Integration Tasks

- [ ] **TASK-006**: Setup background service
  - Configure headless execution
  - Implement startup and shutdown logic
  - Handle system events gracefully

- [ ] **TASK-007**: Implement logging system
  - Create optional debug log file
  - Add error handling and reporting
  - Include timestamps and context in logs

## Testing Tasks

- [ ] **TASK-008**: Create test cases
  - Test with various applications (Notepad, Chrome, etc.)
  - Verify accuracy of caret coordinates
  - Test edge cases and error conditions

- [ ] **TASK-009**: Performance testing
  - Verify CPU usage under 1%
  - Check memory footprint stays under 30MB
  - Test update frequency and latency

## Packaging and Distribution

- [ ] **TASK-010**: Configure self-contained deployment
  - Package as standalone executable
  - Ensure no admin rights required
  - Verify it works without installation

- [ ] **TASK-011**: Create documentation
  - Write usage instructions
  - Document configuration options
  - Include troubleshooting information 