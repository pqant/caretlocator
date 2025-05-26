# Caret Tracker Application

## Overview
This project is a Windows console application that tracks the caret (cursor) position in text input fields across different applications. It logs the caret position, window title, and other relevant information to a JSON file.

## Completed Features
- **Caret Position Tracking**: The application uses Windows API to track the caret position in text input fields.
- **Window Change Detection**: The application detects when the active window changes and logs the window title.
- **Configuration Management**: The application loads configuration from a JSON file, allowing customization of update interval, output path, and debug mode.
- **Logging**: The application logs information to the Windows Event Log and a JSON file.
- **Debug Mode**: The application can be run in debug mode, which shows the console window and logs additional information.
- **Administrator Mode**: The application can be run as administrator using the `-Verb RunAs` parameter in the PowerShell script.

## Limitations
- The application does not track caret position in modern applications such as browsers (Edge, Chrome, etc.) due to their security restrictions.
- The application requires administrator privileges to run.

## Future Improvements
- Implement a browser extension to track caret position in modern applications.
- Add support for more applications and input fields.
- Improve error handling and logging.
- Convert to a proper Windows Service for better system integration.

## Usage
1. Start the application using the `start_caret_tracker.ps1` script.
2. The application will log caret position and window information to the specified output file.
3. To stop the application, press Ctrl+C in the console window or close the window.

## Configuration
The application configuration is stored in a JSON file. The following settings can be customized:
- `UpdateIntervalMs`: The interval in milliseconds between caret position updates.
- `OutputPath`: The path to the output JSON file.
- `DebugMode`: Whether to run the application in debug mode.

## License
This project is licensed under the MIT License. 