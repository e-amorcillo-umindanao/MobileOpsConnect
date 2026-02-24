import os
import re

# Base directory to search for .cshtml files
base_dir = r"c:\Users\Evan\OneDrive\Desktop\IT15 Project\MobileOpsConnect\MobileOpsConnect"

# Regex map: (Pattern, Replacement)
# Using word boundaries \b to ensure we only match whole classes
class_map = [
    # Flexbox
    (r'\bd-flex\b', 'flex'),
    (r'\bd-inline-flex\b', 'inline-flex'),
    (r'\bflex-column\b', 'flex-col'),
    (r'\bflex-row\b', 'flex-row'),
    (r'\bjustify-content-center\b', 'justify-center'),
    (r'\bjustify-content-between\b', 'justify-between'),
    (r'\bjustify-content-end\b', 'justify-end'),
    (r'\balign-items-center\b', 'items-center'),
    (r'\balign-items-start\b', 'items-start'),
    (r'\balign-items-end\b', 'items-end'),
    
    # Grid & Columns
    (r'\brow\b', 'flex flex-wrap -mx-3'),
    (r'\bcol-12\b', 'w-full px-3'),
    (r'\bcol-md-12\b', 'w-full md:w-full px-3'),
    (r'\bcol-md-10\b', 'w-full md:w-5/6 px-3'),
    (r'\bcol-md-9\b', 'w-full md:w-3/4 px-3'),
    (r'\bcol-md-8\b', 'w-full md:w-2/3 px-3'),
    (r'\bcol-md-6\b', 'w-full md:w-1/2 px-3'),
    (r'\bcol-md-4\b', 'w-full md:w-1/3 px-3'),
    (r'\bcol-md-3\b', 'w-full md:w-1/4 px-3'),
    (r'\bcol-lg-12\b', 'w-full lg:w-full px-3'),
    (r'\bcol-lg-8\b', 'w-full lg:w-2/3 px-3'),
    (r'\bcol-lg-6\b', 'w-full lg:w-1/2 px-3'),
    (r'\bcol-lg-4\b', 'w-full lg:w-1/3 px-3'),
    
    # Margins (mb, mt, me, ms, my, mx)
    (r'\bmb-0\b', 'mb-0'), (r'\bmb-1\b', 'mb-1'), (r'\bmb-2\b', 'mb-2'), 
    (r'\bmb-3\b', 'mb-4'), (r'\bmb-4\b', 'mb-6'), (r'\bmb-5\b', 'mb-8'),
    
    (r'\bmt-0\b', 'mt-0'), (r'\bmt-1\b', 'mt-1'), (r'\bmt-2\b', 'mt-2'), 
    (r'\bmt-3\b', 'mt-4'), (r'\bmt-4\b', 'mt-6'), (r'\bmt-5\b', 'mt-8'),
    
    (r'\bme-1\b', 'mr-1'), (r'\bme-2\b', 'mr-2'), (r'\bme-3\b', 'mr-4'), 
    (r'\bme-4\b', 'mr-6'), (r'\bme-5\b', 'mr-8'),
    
    (r'\bms-1\b', 'ml-1'), (r'\bms-2\b', 'ml-2'), (r'\bms-3\b', 'ml-4'), 
    (r'\bms-4\b', 'ml-6'), (r'\bms-5\b', 'ml-8'),
    
    (r'\bmy-1\b', 'my-1'), (r'\bmy-2\b', 'my-2'), (r'\bmy-3\b', 'my-4'),
    (r'\bmy-4\b', 'my-6'), (r'\bmy-5\b', 'my-8'),

    (r'\bmx-auto\b', 'mx-auto'),
    
    # Padding
    (r'\bp-1\b', 'p-1'), (r'\bp-2\b', 'p-2'), (r'\bp-3\b', 'p-4'), 
    (r'\bp-4\b', 'p-6'), (r'\bp-5\b', 'p-8'),
    
    (r'\bpy-1\b', 'py-1'), (r'\bpy-2\b', 'py-2'), (r'\bpy-3\b', 'py-4'), 
    (r'\bpy-4\b', 'py-6'), (r'\bpy-5\b', 'py-8'),
    
    (r'\bpx-1\b', 'px-1'), (r'\bpx-2\b', 'px-2'), (r'\bpx-3\b', 'px-4'), 
    (r'\bpx-4\b', 'px-6'), (r'\bpx-5\b', 'px-8'),
    
    # Cards
    (r'\bcard\b', 'bg-white shadow-sm rounded-xl border border-moc-border overflow-hidden'),
    (r'\bcard-header\b', 'px-6 py-4 border-b border-moc-border bg-gray-50/50'),
    (r'\bcard-body\b', 'p-6'),
    (r'\bcard-footer\b', 'px-6 py-4 border-t border-moc-border bg-gray-50/50'),
    (r'\bcard-title\b', 'text-lg font-semibold text-moc-text'),
    
    # Tables
    (r'\btable-responsive\b', 'overflow-x-auto w-full'),
    (r'\btable\b', 'w-full text-left border-collapse'),
    (r'\btable-hover\b', ''), # Handle logically if needed
    (r'\btable-striped\b', ''), 
    (r'\btable-bordered\b', ''),
    
    # Badges/Pills
    (r'\bbadge\b', 'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-medium'),
    (r'\bbg-primary\b', 'bg-blue-100 text-blue-800'),
    (r'\bbg-success\b', 'bg-green-100 text-green-800'),
    (r'\bbg-warning\b', 'bg-yellow-100 text-yellow-800'),
    (r'\bbg-danger\b', 'bg-red-100 text-red-800'),
    (r'\bbg-info\b', 'bg-cyan-100 text-cyan-800'),
    (r'\bbg-secondary\b', 'bg-gray-100 text-gray-800'),
    
    # Text alignments
    (r'\btext-center\b', 'text-center'),
    (r'\btext-start\b', 'text-left'),
    (r'\btext-end\b', 'text-right'),
    
    # Text colors
    (r'\btext-muted\b', 'text-moc-text-muted'),
    (r'\btext-danger\b', 'text-red-500'),
    (r'\btext-success\b', 'text-green-500'),
    (r'\btext-warning\b', 'text- желтый-500'),
    (r'\btext-primary\b', 'text-moc-primary'),
    
    # Font weights
    (r'\bfw-bold\b', 'font-bold'),
    (r'\bfw-semibold\b', 'font-semibold'),
    (r'\bfw-normal\b', 'font-normal'),
    
    # Display & Visibility
    (r'\bd-none\b', 'hidden'),
    (r'\bd-block\b', 'block'),
    (r'\bd-md-block\b', 'md:block'),
    (r'\bd-md-none\b', 'md:hidden'),
    (r'\bd-lg-block\b', 'lg:block'),
    (r'\bd-lg-none\b', 'lg:hidden'),
    
    # Misc
    (r'\brounded-circle\b', 'rounded-full'),
    (r'\brounded\b', 'rounded-md'),
    (r'\bshadow-sm\b', 'shadow-sm'),
    (r'\bshadow\b', 'shadow-md'),
    (r'\bimg-fluid\b', 'max-w-full h-auto'),
    (r'\bfloat-end\b', 'float-right'),
    (r'\bfloat-start\b', 'float-left'),

    # Forms
    (r'\bform-control\b', 'block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 placeholder:text-gray-400 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:text-sm sm:leading-6'),
    (r'\bform-label\b', 'block text-sm font-medium leading-6 text-gray-900'),
    (r'\bform-select\b', 'block w-full rounded-md border-0 py-1.5 text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 focus:ring-2 focus:ring-inset focus:ring-indigo-600 sm:max-w-xs sm:text-sm sm:leading-6'),

    # Buttons
    (r'\bbtn btn-primary\b', 'rounded-md bg-indigo-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-indigo-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600'),
    (r'\bbtn btn-outline-primary\b', 'rounded-md bg-white px-3 py-2 text-sm font-semibold text-indigo-600 shadow-sm ring-1 ring-inset ring-indigo-600 hover:bg-gray-50'),
    (r'\bbtn btn-secondary\b', 'rounded-md bg-white px-3 py-2 text-sm font-semibold text-gray-900 shadow-sm ring-1 ring-inset ring-gray-300 hover:bg-gray-50'),
    (r'\bbtn btn-success\b', 'rounded-md bg-green-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-green-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-green-600'),
    (r'\bbtn btn-danger\b', 'rounded-md bg-red-600 px-3 py-2 text-sm font-semibold text-white shadow-sm hover:bg-red-500 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-red-600'),
    (r'\bbtn\b', 'inline-flex justify-center rounded-md px-3 py-2 text-sm font-semibold shadow-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2'),
    (r'\bbtn-sm\b', 'px-2 py-1 text-xs'),
    (r'\bbtn-lg\b', 'px-4 py-2 text-base'),
]

files_processed = 0
replacements_made = 0

for root, _, files in os.walk(base_dir):
    for file in files:
        if file.endswith('.cshtml'):
            file_path = os.path.join(root, file)
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()

                original_content = content
                
                # Apply regex replacements within class attributes
                # We use a function to process only the content inside class="..."
                def replacer(match):
                    class_string = match.group(1)
                    for pattern, replacement in class_map:
                        # Only replace if the word matches exactly
                        class_string = re.sub(pattern, replacement, class_string)
                    # Clean up multiple spaces that might result from replacements
                    class_string = re.sub(r'\s+', ' ', class_string).strip()
                    return f'class="{class_string}"'

                new_content = re.sub(r'class="([^"]+)"', replacer, content)

                if new_content != original_content:
                    with open(file_path, 'w', encoding='utf-8') as f:
                        f.write(new_content)
                    files_processed += 1
                    print(f"Updated: {file_path}")
            except Exception as e:
                print(f"Error processing {file_path}: {e}")

print(f"\nMigration complete. Processed {files_processed} files.")
