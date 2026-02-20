const fs = require('fs');
const PDFExtract = require('pdf.js-extract').PDFExtract;
const pdfExtract = new PDFExtract();
const options = {}; /* see below */
pdfExtract.extract('c:/Users/Evan/OneDrive/Desktop/IT15 Project/MobileOpsConnect/documents/3rd_Deliverables_CRUD_ADMIN_CLIENT_AMORCILLO.pdf', options, (err, data) => {
    if (err) return console.log(err);
    data.pages.forEach(page => {
        console.log(`--- Page ${page.pageInfo.num} ---`);
        page.content.forEach(item => {
            if (item.str.trim() !== '') {
                console.log(item.str);
            }
        });
    });
});
