import PyPDF2
import sys

pdf_path = r'c:\Users\Evan\OneDrive\Desktop\IT15 Project\MobileOpsConnect\documents\3rd_Deliverables_CRUD_ADMIN_CLIENT - Tagged.pdf'
reader = PyPDF2.PdfReader(pdf_path)
print(f'Total pages: {len(reader.pages)}')
for i, page in enumerate(reader.pages):
    text = page.extract_text()
    print(f'\n=== PAGE {i+1} ===')
    print(text)
