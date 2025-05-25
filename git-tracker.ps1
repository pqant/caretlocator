# Git Tracker Script for Caret Tracker Service
# This script automatically tracks changes and commits them to git with descriptive messages
# Note: All comments in code and commit messages must be in English

# Function to detect which files have been modified
function Get-ModifiedFiles {
    # Get list of modified files using git status
    $modifiedFiles = git status --porcelain | Where-Object { $_ -match '^\s*[AM]' }
    
    # Extract just the filenames
    $fileNames = $modifiedFiles | ForEach-Object {
        $_.Substring(2).Trim()
    }
    
    return $fileNames
}

# Function to determine task ID from file content or name
function Get-TaskId {
    param (
        [string]$fileName
    )
    
    # Default task ID if we can't determine one
    $taskId = "000"
    
    # Read file content to look for task ID
    if (Test-Path $fileName) {
        $content = Get-Content $fileName -Raw
        
        # Try to find task ID in the content (TASK-NNN format)
        if ($content -match "TASK-(\d{3})") {
            $taskId = $matches[1]
        }
        
        # If it's a code file implementing a specific task
        elseif ($fileName -match "CaretTracker|Position|Detection") {
            $taskId = "003"  # Caret position detection
        }
        elseif ($fileName -match "Config|Settings") {
            $taskId = "002"  # Configuration
        }
        elseif ($fileName -match "JSON|Output|Serialization") {
            $taskId = "004"  # JSON output
        }
        elseif ($fileName -match "Timer|Tracking") {
            $taskId = "005"  # Timer tracking
        }
        elseif ($fileName -match "Service|Background") {
            $taskId = "006"  # Background service
        }
        elseif ($fileName -match "Log|Debug") {
            $taskId = "007"  # Logging
        }
    }
    
    return $taskId
}

# Function to generate commit message based on changes
function Get-CommitMessage {
    param (
        [string[]]$files
    )
    
    # Default message
    $message = "Update project files"
    $taskId = "000"
    
    # If only one file is changed, make a more specific message
    if ($files.Count -eq 1) {
        $file = $files[0]
        $fileName = Split-Path $file -Leaf
        $taskId = Get-TaskId -fileName $file
        
        # Generate message based on file type
        if ($fileName -match "\.cs$") {
            $message = "Update $fileName code"
        }
        elseif ($fileName -match "\.md$") {
            $message = "Update documentation in $fileName"
        }
        elseif ($fileName -match "\.json$") {
            $message = "Update configuration in $fileName"
        }
        elseif ($fileName -match "\.csproj$") {
            $message = "Update project settings"
        }
        else {
            $message = "Update $fileName"
        }
    }
    else {
        # Multiple files changed
        $codeFiles = $files | Where-Object { $_ -match "\.cs$" }
        $docFiles = $files | Where-Object { $_ -match "\.md$" }
        $configFiles = $files | Where-Object { $_ -match "\.json$" }
        
        if ($codeFiles.Count -gt 0 -and $docFiles.Count -eq 0 -and $configFiles.Count -eq 0) {
            $message = "Update code files"
            # Try to get task ID from first code file
            if ($codeFiles.Count -gt 0) {
                $taskId = Get-TaskId -fileName $codeFiles[0]
            }
        }
        elseif ($docFiles.Count -gt 0 -and $codeFiles.Count -eq 0) {
            $message = "Update documentation"
        }
        elseif ($configFiles.Count -gt 0 -and $codeFiles.Count -eq 0 -and $docFiles.Count -eq 0) {
            $message = "Update configuration files"
        }
    }
    
    return @{
        Message = $message
        TaskId = $taskId
    }
}

# Function to commit all changes
function Commit-AllChanges {
    # Get modified files
    $modifiedFiles = Get-ModifiedFiles
    
    if ($modifiedFiles.Count -eq 0) {
        Write-Host "No modified files found to commit" -ForegroundColor Yellow
        return
    }
    
    # Generate commit message
    $commitInfo = Get-CommitMessage -files $modifiedFiles
    $message = $commitInfo.Message
    $taskId = $commitInfo.TaskId
    
    # Create full commit message with task ID
    $fullMessage = "TASK-${taskId}: $message"
    
    # Add all files to staging
    foreach ($file in $modifiedFiles) {
        git add $file
        Write-Host "Added $file to staging" -ForegroundColor Cyan
    }
    
    # Commit changes
    git commit -m $fullMessage
    
    Write-Host "Committed changes with message: $fullMessage" -ForegroundColor Green
    Write-Host "Files committed: $($modifiedFiles -join ', ')" -ForegroundColor Green
}

# Execute the commit function
Commit-AllChanges

# Instructions for use:
# 1. After making changes, simply run this script
# 2. It will automatically detect changed files
# 3. Generate an appropriate commit message
# 4. Commit all changes with a standardized message
#
# Example:
# .\git-tracker.ps1 