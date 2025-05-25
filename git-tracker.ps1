# Git Tracker Script for Caret Tracker Service
# This script analyzes changes and creates meaningful commit messages
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

# Function to analyze changes and generate detailed commit message
function Get-CommitMessage {
    param (
        [string[]]$files
    )
    
    # Get task ID from the most relevant file
    $taskId = "000"
    if ($files.Count -gt 0) {
        $taskId = Get-TaskId -fileName $files[0]
    }
    
    # Analyze changes in each file
    $changeDetails = @()
    foreach ($file in $files) {
        $diff = git diff $file
        $content = Get-Content $file -Raw
        
        # Analyze changes based on file type and content
        if ($file -match "\.cs$") {
            if ($diff -match "class\s+\w+") {
                $className = [regex]::Match($diff, "class\s+(\w+)").Groups[1].Value
                $changeDetails += "Added $className class for handling specific functionality"
            }
            if ($diff -match "using\s+\w+") {
                $namespaces = [regex]::Matches($diff, "using\s+([\w\.]+)") | ForEach-Object { $_.Groups[1].Value }
                $changeDetails += "Added required namespaces: $($namespaces -join ', ')"
            }
            if ($diff -match "async\s+Task") {
                $methodName = [regex]::Match($diff, "async\s+Task\s+(\w+)").Groups[1].Value
                $changeDetails += "Implemented async $methodName method for better performance"
            }
            if ($diff -match "\[DllImport") {
                $changeDetails += "Added Windows API imports for system-level functionality"
            }
            if ($diff -match "try\s*{") {
                $changeDetails += "Added error handling for better reliability"
            }
        }
        elseif ($file -match "\.csproj$") {
            if ($diff -match "<PropertyGroup>") {
                $properties = [regex]::Matches($diff, "<(\w+)>([^<]+)</\1>") | ForEach-Object { "$($_.Groups[1].Value)=$($_.Groups[2].Value)" }
                $changeDetails += "Updated project properties: $($properties -join ', ')"
            }
            if ($diff -match "<PackageReference") {
                $packages = [regex]::Matches($diff, "Include=`"([^`"]+)`"") | ForEach-Object { $_.Groups[1].Value }
                $changeDetails += "Added/updated NuGet packages: $($packages -join ', ')"
            }
        }
        elseif ($file -match "\.json$") {
            $changeDetails += "Updated configuration settings for application behavior"
        }
        elseif ($file -match "\.md$") {
            $changeDetails += "Updated documentation with latest changes"
        }
    }
    
    # Generate the commit message
    $message = switch ($taskId) {
        "001" { 
            "Initialize project structure`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "002" { 
            "Implement configuration system`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "003" { 
            "Add caret position detection`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "004" { 
            "Create JSON output system`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "005" { 
            "Implement periodic tracking`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "006" { 
            "Setup background service`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        "007" { 
            "Add logging system`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
        }
        default { 
            "Update project files`n" + 
            ($changeDetails | ForEach-Object { "- $_" }) -join "`n"
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
# 2. It will analyze the changes and create a detailed commit message
# 3. The message will explain what was changed and why
# 4. Commit all changes with the generated message
#
# Example:
# .\git-tracker.ps1 