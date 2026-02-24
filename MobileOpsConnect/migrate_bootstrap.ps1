$baseDir = "c:\Users\Evan\OneDrive\Desktop\IT15 Project\MobileOpsConnect\MobileOpsConnect"

$classMap = @(
    # Flexbox
    @{Pattern='\bd-flex\b'; Replacement='flex'},
    @{Pattern='\bd-inline-flex\b'; Replacement='inline-flex'},
    @{Pattern='\bflex-column\b'; Replacement='flex-col'},
    @{Pattern='\bflex-row\b'; Replacement='flex-row'},
    @{Pattern='\bjustify-content-center\b'; Replacement='justify-center'},
    @{Pattern='\bjustify-content-between\b'; Replacement='justify-between'},
    @{Pattern='\bjustify-content-end\b'; Replacement='justify-end'},
    @{Pattern='\balign-items-center\b'; Replacement='items-center'},
    @{Pattern='\balign-items-start\b'; Replacement='items-start'},
    @{Pattern='\balign-items-end\b'; Replacement='items-end'},
    
    # Grid & Columns
    @{Pattern='\brow\b'; Replacement='flex flex-wrap -mx-3'},
    @{Pattern='\bcol-12\b'; Replacement='w-full px-3'},
    @{Pattern='\bcol-md-12\b'; Replacement='w-full px-3'},
    @{Pattern='\bcol-md-10\b'; Replacement='w-full md:w-5/6 px-3'},
    @{Pattern='\bcol-md-9\b'; Replacement='w-full md:w-3/4 px-3'},
    @{Pattern='\bcol-md-8\b'; Replacement='w-full md:w-2/3 px-3'},
    @{Pattern='\bcol-md-6\b'; Replacement='w-full md:w-1/2 px-3'},
    @{Pattern='\bcol-md-4\b'; Replacement='w-full md:w-1/3 px-3'},
    @{Pattern='\bcol-md-3\b'; Replacement='w-full md:w-1/4 px-3'},
    @{Pattern='\bcol-lg-12\b'; Replacement='w-full px-3'},
    @{Pattern='\bcol-lg-8\b'; Replacement='w-full lg:w-2/3 px-3'},
    @{Pattern='\bcol-lg-6\b'; Replacement='w-full lg:w-1/2 px-3'},
    @{Pattern='\bcol-lg-4\b'; Replacement='w-full lg:w-1/3 px-3'},
    
    # Margins
    @{Pattern='\bmb-0\b'; Replacement='mb-0'}, @{Pattern='\bmb-1\b'; Replacement='mb-1'}, @{Pattern='\bmb-2\b'; Replacement='mb-2'}, 
    @{Pattern='\bmb-3\b'; Replacement='mb-4'}, @{Pattern='\bmb-4\b'; Replacement='mb-6'}, @{Pattern='\bmb-5\b'; Replacement='mb-8'},
    
    @{Pattern='\bmt-0\b'; Replacement='mt-0'}, @{Pattern='\bmt-1\b'; Replacement='mt-1'}, @{Pattern='\bmt-2\b'; Replacement='mt-2'}, 
    @{Pattern='\bmt-3\b'; Replacement='mt-4'}, @{Pattern='\bmt-4\b'; Replacement='mt-6'}, @{Pattern='\bmt-5\b'; Replacement='mt-8'},
    
    @{Pattern='\bme-1\b'; Replacement='mr-1'}, @{Pattern='\bme-2\b'; Replacement='mr-2'}, @{Pattern='\bme-3\b'; Replacement='mr-4'}, 
    @{Pattern='\bme-4\b'; Replacement='mr-6'}, @{Pattern='\bme-5\b'; Replacement='mr-8'},
    
    @{Pattern='\bms-1\b'; Replacement='ml-1'}, @{Pattern='\bms-2\b'; Replacement='ml-2'}, @{Pattern='\bms-3\b'; Replacement='ml-4'}, 
    @{Pattern='\bms-4\b'; Replacement='ml-6'}, @{Pattern='\bms-5\b'; Replacement='ml-8'},
    
    @{Pattern='\bmy-1\b'; Replacement='my-1'}, @{Pattern='\bmy-2\b'; Replacement='my-2'}, @{Pattern='\bmy-3\b'; Replacement='my-4'},
    @{Pattern='\bmy-4\b'; Replacement='my-6'}, @{Pattern='\bmy-5\b'; Replacement='my-8'},

    @{Pattern='\bmx-auto\b'; Replacement='mx-auto'},
    
    # Padding
    @{Pattern='\bp-1\b'; Replacement='p-1'}, @{Pattern='\bp-2\b'; Replacement='p-2'}, @{Pattern='\bp-3\b'; Replacement='p-4'}, 
    @{Pattern='\bp-4\b'; Replacement='p-6'}, @{Pattern='\bp-5\b'; Replacement='p-8'},
    
    @{Pattern='\bpy-1\b'; Replacement='py-1'}, @{Pattern='\bpy-2\b'; Replacement='py-2'}, @{Pattern='\bpy-3\b'; Replacement='py-4'}, 
    @{Pattern='\bpy-4\b'; Replacement='py-6'}, @{Pattern='\bpy-5\b'; Replacement='py-8'},
    
    @{Pattern='\bpx-1\b'; Replacement='px-1'}, @{Pattern='\bpx-2\b'; Replacement='px-2'}, @{Pattern='\bpx-3\b'; Replacement='px-4'}, 
    @{Pattern='\bpx-4\b'; Replacement='px-6'}, @{Pattern='\bpx-5\b'; Replacement='px-8'},
    
    # Cards
    @{Pattern='\bcard\b'; Replacement='bg-white shadow-sm rounded-xl border border-moc-border overflow-hidden'},
    @{Pattern='\bcard-header\b'; Replacement='px-6 py-4 border-b border-moc-border bg-gray-50'},
    @{Pattern='\bcard-body\b'; Replacement='p-6'},
    @{Pattern='\bcard-footer\b'; Replacement='px-6 py-4 border-t border-moc-border bg-gray-50'},
    @{Pattern='\bcard-title\b'; Replacement='text-lg font-semibold text-moc-text'},
    
    # Tables
    @{Pattern='\btable-responsive\b'; Replacement='overflow-x-auto w-full'},
    @{Pattern='\btable\b'; Replacement='w-full text-left border-collapse'},
    @{Pattern='\btable-hover\b'; Replacement=''},
    @{Pattern='\btable-striped\b'; Replacement=''}, 
    @{Pattern='\btable-bordered\b'; Replacement=''},
    
    # Badges/Pills
    @{Pattern='\bbadge\b'; Replacement='inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium'},
    @{Pattern='\bbg-primary\b'; Replacement='bg-blue-100 text-blue-800'},
    @{Pattern='\bbg-success\b'; Replacement='bg-green-100 text-green-800'},
    @{Pattern='\bbg-warning\b'; Replacement='bg-yellow-100 text-yellow-800'},
    @{Pattern='\bbg-danger\b'; Replacement='bg-red-100 text-red-800'},
    @{Pattern='\bbg-info\b'; Replacement='bg-cyan-100 text-cyan-800'},
    @{Pattern='\bbg-secondary\b'; Replacement='bg-gray-100 text-gray-800'},
    
    # Text alignments
    @{Pattern='\btext-center\b'; Replacement='text-center'},
    @{Pattern='\btext-start\b'; Replacement='text-left'},
    @{Pattern='\btext-end\b'; Replacement='text-right'},
    
    # Text colors
    @{Pattern='\btext-muted\b'; Replacement='text-moc-text-muted'},
    @{Pattern='\btext-danger\b'; Replacement='text-red-500'},
    @{Pattern='\btext-success\b'; Replacement='text-green-500'},
    @{Pattern='\btext-warning\b'; Replacement='text-yellow-500'},
    @{Pattern='\btext-primary\b'; Replacement='text-moc-primary'},
    
    # Font weights
    @{Pattern='\bfw-bold\b'; Replacement='font-bold'},
    @{Pattern='\bfw-semibold\b'; Replacement='font-semibold'},
    @{Pattern='\bfw-normal\b'; Replacement='font-normal'},
    
    # Display & Visibility
    @{Pattern='\bd-none\b'; Replacement='hidden'},
    @{Pattern='\bd-block\b'; Replacement='block'},
    @{Pattern='\bd-md-block\b'; Replacement='md:block'},
    @{Pattern='\bd-md-none\b'; Replacement='md:hidden'},
    @{Pattern='\bd-lg-block\b'; Replacement='lg:block'},
    @{Pattern='\bd-lg-none\b'; Replacement='lg:hidden'},
    
    # Misc
    @{Pattern='\brounded-circle\b'; Replacement='rounded-full'},
    @{Pattern='\brounded\b'; Replacement='rounded-md'},
    @{Pattern='\bshadow-sm\b'; Replacement='shadow-sm'},
    @{Pattern='\bshadow\b'; Replacement='shadow-md'},
    @{Pattern='\bimg-fluid\b'; Replacement='max-w-full h-auto'},
    @{Pattern='\bfloat-end\b'; Replacement='float-right'},
    @{Pattern='\bfloat-start\b'; Replacement='float-left'},

    # Forms
    @{Pattern='\bform-control\b'; Replacement='block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6'},
    @{Pattern='\bform-label\b'; Replacement='block text-sm font-medium leading-6 text-gray-900'},
    @{Pattern='\bform-select\b'; Replacement='block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6'},

    # Buttons
    @{Pattern='\bbtn btn-primary\b'; Replacement='rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600'},
    @{Pattern='\bbtn btn-outline-primary\b'; Replacement='rounded-md bg-white px-3 py-2 text-sm font-semibold text-indigo-600 shadow-sm ring-1 ring-inset ring-indigo-600 hover:bg-gray-50'},
    @{Pattern='\bbtn btn-secondary\b'; Replacement='rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50'},
    @{Pattern='\bbtn btn-success\b'; Replacement='rounded-md bg-green-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-green-600'},
    @{Pattern='\bbtn btn-danger\b'; Replacement='rounded-md bg-red-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600'},
    @{Pattern='\bbtn\b'; Replacement='inline-flex justify-center rounded-md px-3 py-2 text-sm font-semibold shadow-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2'},
    @{Pattern='\bbtn-sm\b'; Replacement='px-2 py-1 text-xs'},
    @{Pattern='\bbtn-lg\b'; Replacement='px-4 py-2 text-base'},
    @{Pattern='\bgap-1\b'; Replacement='gap-1'},
    @{Pattern='\bgap-2\b'; Replacement='gap-2'},
    @{Pattern='\bgap-3\b'; Replacement='gap-4'},
    @{Pattern='\bgap-4\b'; Replacement='gap-6'}
)

$filesProcessed = 0
$csHtmlFiles = Get-ChildItem -Path $baseDir -Filter *.cshtml -Recurse

foreach ($file in $csHtmlFiles) {
    try {
        $content = Get-Content -Path $file.FullName -Raw -Encoding UTF8
        $originalContent = $content

        # Match class="..." specifically
        $regex = [regex]'(class\s*=\s*")([^"]+)(")'
        
        $newContent = $regex.Replace($content, {
            param($match)
            $classString = $match.Groups[2].Value
            
            foreach ($map in $classMap) {
                # Ensure we only replace whole words (classes)
                $classString = [regex]::Replace($classString, "(?<=^|\s)$($map.Pattern)(?=\s|$)", $map.Replacement)
            }
            
            # Clean up extra spaces
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

Write-Host "`nMigration complete. Processed $filesProcessed files."
