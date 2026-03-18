const fs = require('fs');
const path = require('path');

function getControllersAndActions() {
    const validRoutes = {};
    const controllerDir = 'Controllers';
    if (!fs.existsSync(controllerDir)) return validRoutes;
    
    fs.readdirSync(controllerDir).forEach(filename => {
        if (filename.endsWith('Controller.cs')) {
            const controllerName = filename.replace('Controller.cs', '');
            const filepath = path.join(controllerDir, filename);
            const content = fs.readFileSync(filepath, 'utf8');
            
            // Match: public [async Task<]IActionResult[>] MethodName[<...>] (
            const pattern = /public\s+(?:async\s+Task<)?(?:IActionResult|ActionResult)(?:>)?\s+(\w+)/g;
            const actions = new Set();
            let match;
            while ((match = pattern.exec(content)) !== null) {
                actions.add(match[1]);
            }
            validRoutes[controllerName] = Array.from(actions);
        }
    });
    return validRoutes;
}

function scanViewsForLinks() {
    const links = [];
    const viewDir = 'Views';
    if (!fs.existsSync(viewDir)) return links;
    
    function walk(dir) {
        fs.readdirSync(dir).forEach(file => {
            const fullPath = path.join(dir, file);
            if (fs.statSync(fullPath).isDirectory()) {
                walk(fullPath);
            } else if (file.endsWith('.cshtml')) {
                const content = fs.readFileSync(fullPath, 'utf8');
                
                // Extremely crude tag matching
                // just find pairs of asp-controller="..." asp-action="..."
                const tagPattern = /<[^>]+>/g;
                let match;
                while ((match = tagPattern.exec(content)) !== null) {
                    const tag = match[0];
                    const ctrlMatch = tag.match(/asp-controller="([^"]+)"/);
                    const actMatch = tag.match(/asp-action="([^"]+)"/);
                    
                    if (ctrlMatch && actMatch) {
                        links.push({
                            type: 'asp-helper',
                            controller: ctrlMatch[1],
                            action: actMatch[1],
                            file: fullPath
                        });
                    } else if (actMatch && !ctrlMatch) {
                        // Inherit controller from folder
                        const folder = path.basename(path.dirname(fullPath));
                        if (folder !== 'Shared') {
                            links.push({
                                type: 'asp-helper-implicit',
                                controller: folder,
                                action: actMatch[1],
                                file: fullPath
                            });
                        }
                    }
                }
                
                // Find hrefs
                const hrefPattern = /href="(\/?[^"]+)"/g;
                while ((match = hrefPattern.exec(content)) !== null) {
                    const url = match[1];
                    if (!url.startsWith('#') && !url.startsWith('http') && !url.startsWith('mailto:')) {
                        links.push({
                            type: 'href',
                            url: url,
                            file: fullPath
                        });
                    }
                }
            }
        });
    }
    walk(viewDir);
    return links;
}

function scanControllersForRedirects() {
    const redirects = [];
    const controllerDir = 'Controllers';
    if (!fs.existsSync(controllerDir)) return redirects;
    
    fs.readdirSync(controllerDir).forEach(filename => {
        if (filename.endsWith('Controller.cs')) {
            const filepath = path.join(controllerDir, filename);
            const content = fs.readFileSync(filepath, 'utf8');
            
            const p1 = /RedirectToAction\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)/g;
            let match;
            while ((match = p1.exec(content)) !== null) {
                redirects.push({
                    type: 'redirect',
                    controller: match[2],
                    action: match[1],
                    file: filepath
                });
            }
            
            const currentCtrl = filename.replace('Controller.cs', '');
            const p2 = /RedirectToAction\(\s*"([^"]+)"\s*\)/g;
            while ((match = p2.exec(content)) !== null) {
                redirects.push({
                    type: 'redirect',
                    controller: currentCtrl,
                    action: match[1],
                    file: filepath
                });
            }
        }
    });
    return redirects;
}

function analyze() {
    const routes = getControllersAndActions();
    const links = scanViewsForLinks();
    const redirects = scanControllersForRedirects();
    
    const errors = new Set();
    
    links.forEach(link => {
        if (link.type.startsWith('asp-helper')) {
            const c = link.controller;
            const a = link.action;
            if (!routes[c]) {
                errors.add(`404: Controller '${c}' not found. Referenced in ${link.file} (Action: ${a})`);
            } else if (!routes[c].includes(a)) {
                // Ignore generated Index vs empty, etc.
                errors.add(`404: Action '${a}' not found in Controller '${c}'. Referenced in ${link.file}`);
            }
        } else if (link.type === 'href') {
            let url = link.url.split('?')[0]; // strip query
            if (url.startsWith('/')) url = url.substring(1);
            const parts = url.split('/').filter(p => p !== '');
            if (parts.length >= 2) {
                const c = parts[0];
                const a = parts[1];
                // basic casing check
                // This might be flaky, so we just check if controller exists and action exists case-insensitively
                const matchedCtrlKey = Object.keys(routes).find(key => key.toLowerCase() === c.toLowerCase());
                if (matchedCtrlKey) {
                    const matchedAct = routes[matchedCtrlKey].find(act => act.toLowerCase() === a.toLowerCase());
                    if (!matchedAct) {
                        errors.add(`Possible 404: Href '/${c}/${a}' -> Action '${a}' not found in Controller '${matchedCtrlKey}'. Referenced in ${link.file}`);
                    }
                }
            }
        }
    });
    
    redirects.forEach(link => {
        const c = link.controller;
        const a = link.action;
        if (!routes[c]) {
            errors.add(`404: Redirect Controller '${c}' not found. Referenced in ${link.file} (Action: ${a})`);
        } else if (!routes[c].includes(a)) {
            errors.add(`404: Redirect Action '${a}' not found in Controller '${c}'. Referenced in ${link.file}`);
        }
    });

    errors.forEach(e => console.log(e));
}

analyze();
