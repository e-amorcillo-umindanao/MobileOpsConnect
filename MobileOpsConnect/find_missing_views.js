const fs = require('fs');
const path = require('path');

function getControllers() {
    const controllers = [];
    const dir = 'Controllers';
    if (!fs.existsSync(dir)) return controllers;
    fs.readdirSync(dir).forEach(file => {
        if (file.endsWith('Controller.cs')) {
            controllers.push({
                name: file.replace('Controller.cs', ''),
                path: path.join(dir, file)
            });
        }
    });
    return controllers;
}

function analyzeViews() {
    const controllers = getControllers();
    const missingViews = [];

    controllers.forEach(ctrl => {
        const content = fs.readFileSync(ctrl.path, 'utf8');
        // Match return View(); or return View("Name");
        // Also captures return PartialView(...)
        const viewPattern = /return\s+(?:Partial)?View\((?:"([^"]+)")?/g;
        let match;
        while ((match = viewPattern.exec(content)) !== null) {
            let viewName = match[1];
            const isImplicit = !viewName;
            
            if (isImplicit) {
                // Find the method name
                // This is crude: find the last 'public ... MethodName(' before the match
                const upToMatch = content.substring(0, match.index);
                const methodMatch = [...upToMatch.matchAll(/public\s+(?:async\s+Task<)?(?:IActionResult|ActionResult|PartialViewResult)(?:>)?\s+(\w+)\(/g)].pop();
                if (methodMatch) {
                    viewName = methodMatch[1];
                    // Strip Async suffix for view file matching
                    if (viewName.endsWith('Async')) viewName = viewName.slice(0, -5);
                }
            }

            if (viewName) {
                const viewPath = path.join('Views', ctrl.name, viewName + '.cshtml');
                const sharedPath = path.join('Views', 'Shared', viewName + '.cshtml');
                
                if (!fs.existsSync(viewPath) && !fs.existsSync(sharedPath)) {
                    // Check if it's a redirect or something else? No, this is return View.
                    // Some views might be in subfolders if Areas are used, but the script is basic.
                    missingViews.push({
                        controller: ctrl.name,
                        view: viewName,
                        path: viewPath,
                        file: ctrl.path
                    });
                }
            }
        }
    });

    missingViews.forEach(m => {
        console.log(`Potential 500: View '${m.view}' missing for ${m.controller}. Expected at ${m.path}`);
    });
}

analyzeViews();
