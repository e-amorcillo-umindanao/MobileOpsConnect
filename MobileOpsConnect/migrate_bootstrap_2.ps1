$baseDir = "c:\Users\Evan\OneDrive\Desktop\IT15 Project\MobileOpsConnect\MobileOpsConnect"

$classMap = @(
    # Columns
    @{Pattern = '\bcol-1\b'; Replacement = 'w-full md:w-1/12 px-3' },
    @{Pattern = '\bcol-2\b'; Replacement = 'w-full md:w-1/6 px-3' },
    @{Pattern = '\bcol-3\b'; Replacement = 'w-full md:w-1/4 px-3' },
    @{Pattern = '\bcol-4\b'; Replacement = 'w-full md:w-1/3 px-3' },
    @{Pattern = '\bcol-5\b'; Replacement = 'w-full md:w-5/12 px-3' },
    @{Pattern = '\bcol-6\b'; Replacement = 'w-full md:w-1/2 px-3' },
    @{Pattern = '\bcol-7\b'; Replacement = 'w-full md:w-7/12 px-3' },
    @{Pattern = '\bcol-8\b'; Replacement = 'w-full md:w-2/3 px-3' },
    @{Pattern = '\bcol-9\b'; Replacement = 'w-full md:w-3/4 px-3' },
    @{Pattern = '\bcol-10\b'; Replacement = 'w-full md:w-5/6 px-3' },
    @{Pattern = '\bcol-11\b'; Replacement = 'w-full md:w-11/12 px-3' },
    
    @{Pattern = '\bcol-sm-[0-9]+\b'; Replacement = 'w-full sm:w-1/2 px-3' },
    @{Pattern = '\bcol-md-[0-9]+\b'; Replacement = 'w-full md:w-1/2 px-3' },
    @{Pattern = '\bcol-lg-[0-9]+\b'; Replacement = 'w-full lg:w-1/2 px-3' },
    @{Pattern = '\bcol-xl-[0-9]+\b'; Replacement = 'w-full xl:w-1/2 px-3' },
    
    # Layout and grids
    @{Pattern = '\bd-grid\b'; Replacement = 'grid' },
    @{Pattern = '\bg-1\b'; Replacement = 'gap-2' },
    @{Pattern = '\bg-2\b'; Replacement = 'gap-4' },
    @{Pattern = '\bg-3\b'; Replacement = 'gap-6' },
    @{Pattern = '\bg-4\b'; Replacement = 'gap-8' },
    
    @{Pattern = '\bjustify-content-start\b'; Replacement = 'justify-start' },
    @{Pattern = '\balign-items-baseline\b'; Replacement = 'items-baseline' },
    @{Pattern = '\balign-middle\b'; Replacement = 'align-middle' },
    
    # Flex utilities
    @{Pattern = '\bflex-fill\b'; Replacement = 'flex-1' },
    @{Pattern = '\bflex-grow-1\b'; Replacement = 'flex-grow' },
    
    # Text utilities
    @{Pattern = '\btext-truncate\b'; Replacement = 'truncate' },
    @{Pattern = '\btext-decoration-none\b'; Replacement = 'no-underline' },
    @{Pattern = '\btext-uppercase\b'; Replacement = 'uppercase' },

    # Forms / Inputs
    @{Pattern = '\binput-group\b'; Replacement = 'flex w-full items-stretch' },
    @{Pattern = '\binput-group-lg\b'; Replacement = '' },
    @{Pattern = '\binput-group-text\b'; Replacement = 'flex items-center px-4 py-2 bg-gray-50 border border-gray-300 text-gray-500 text-sm font-medium' },
    @{Pattern = '\bcol-form-label\b'; Replacement = 'block text-sm font-medium text-gray-700' },

    # Common mis-mapped button text
    @{Pattern = '\btext-white\b'; Replacement = 'text-white' },
    
    # Min widths
    @{Pattern = '\bmin-width-0\b'; Replacement = 'min-w-0' }
)

$filesProcessed = 0
$csHtmlFiles = Get-ChildItem -Path $baseDir -Filter *.cshtml -Recurse

foreach ($file in $csHtmlFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $originalContent = $content

        $regex = [regex]'(class\s*=\s*")([^"]+)(")'
        
        $newContent = $regex.Replace($content, {
                param($match)
                $classString = $match.Groups[2].Value
            
                foreach ($map in $classMap) {
                    $classString = [regex]::Replace($classString, "(?<=^|\s)$($map.Pattern)(?=\s|$)", $map.Replacement)
                }
            
                $classString = [regex]::Replace($classString, '\s+', ' ').Trim()
            
                return "class=`"$classString`""
            })

        if ($newContent -cne $originalContent) {
            Set-Content -Path $file.FullName -Value $newContent -Encoding UTF8
            $filesProcessed++
            Write-Host "Updated: $($file.FullName)"
        }
    }
    catch {
        Write-Host "Error processing $($file.FullName): $_"
    }
}

Write-Host "`nSecondary Migration complete. Processed $filesProcessed files."
