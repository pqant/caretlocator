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

# Function to analyze changes using AI
function Get-AIAnalysis {
    param (
        [string[]]$files
    )
    
    $analysis = @()
    
    foreach ($file in $files) {
        # Get the diff for the file
        $diff = git diff $file
        
        # Get the file content
        $content = Get-Content $file -Raw
        
        # Create a prompt for AI analysis
        $prompt = @"
Analyze these changes and provide a brief, meaningful description of what was changed and why.
Focus on the purpose and impact of the changes, not just technical details.

File: $file
Changes:
$diff

Current Content:
$content

Provide a brief analysis in this format:
PURPOSE: [Why these changes were made]
IMPACT: [What these changes accomplish]
"@

        # Here you would integrate with an AI service
        # For now, we'll use a simple analysis based on patterns
        $purpose = ""
        $impact = ""
        
        if ($file -match "\.cs$") {
            if ($diff -match "class\s+\w+") {
                $purpose = "Implement new functionality"
                $impact = "Adds new class to handle specific feature"
            }
            elseif ($diff -match "using\s+\w+") {
                $purpose = "Add required dependencies"
                $impact = "Enables new functionality through external libraries"
            }
            elseif ($diff -match "async\s+Task") {
                $purpose = "Implement asynchronous operation"
                $impact = "Improves performance and responsiveness"
            }
            elseif ($diff -match "\[DllImport") {
                $purpose = "Integrate with Windows API"
                $impact = "Enables system-level functionality"
            }
            else {
                $purpose = "Refine existing implementation"
                $impact = "Improves code quality and functionality"
            }
        }
        elseif ($file -match "\.csproj$") {
            if ($diff -match "<PropertyGroup>") {
                $purpose = "Configure project settings"
                $impact = "Sets up build and runtime parameters"
            }
            elseif ($diff -match "<PackageReference") {
                $purpose = "Add project dependencies"
                $impact = "Enables required functionality through NuGet packages"
            }
            else {
                $purpose = "Update project configuration"
                $impact = "Modifies build and deployment settings"
            }
        }
        elseif ($file -match "\.json$") {
            $purpose = "Update configuration"
            $impact = "Modifies application behavior and settings"
        }
        elseif ($file -match "\.md$") {
            $purpose = "Update documentation"
            $impact = "Improves project documentation and clarity"
        }
        
        $analysis += @{
            File = $file
            Purpose = $purpose
            Impact = $impact
        }
    }
    
    return $analysis
}

# Function to generate commit message based on AI analysis
function Get-CommitMessage {
    param (
        [string[]]$files
    )
    
    # Get AI analysis of changes
    $analysis = Get-AIAnalysis -files $files
    
    # Get task ID from the most relevant file
    $taskId = "000"
    if ($files.Count -gt 0) {
        $taskId = Get-TaskId -fileName $files[0]
    }
    
    # Generate meaningful commit message
    $message = ""
    if ($analysis.Count -eq 1) {
        $a = $analysis[0]
        $message = "$($a.Purpose) - $($a.Impact)"
    }
    else {
        $purposes = $analysis | ForEach-Object { $_.Purpose } | Select-Object -Unique
        $impacts = $analysis | ForEach-Object { $_.Impact } | Select-Object -Unique
        $message = "Multiple changes: $($purposes -join ', ') - $($impacts -join ', ')"
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