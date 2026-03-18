import os
import re

def get_controllers_and_actions():
    valid_routes = {}
    controller_dir = 'Controllers'
    if not os.path.exists(controller_dir):
        return valid_routes
        
    for filename in os.listdir(controller_dir):
        if filename.endswith('Controller.cs'):
            controller_name = filename.replace('Controller.cs', '')
            filepath = os.path.join(controller_dir, filename)
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
                # Find methods returning [Task<]IActionResult[>]
                # Match: public [async Task<]IActionResult[>] MethodName[<...>] (
                pattern = r'public\s+(?:async\s+Task<)?(?:IActionResult|ActionResult)(?:>)?\s+(\w+)'
                actions = set(re.findall(pattern, content))
                valid_routes[controller_name] = actions
    return valid_routes

def scan_views_for_links():
    links = []
    view_dir = 'Views'
    if not os.path.exists(view_dir):
        return links
        
    for root, _, files in os.walk(view_dir):
        for filename in files:
            if filename.endswith('.cshtml'):
                filepath = os.path.join(root, filename)
                with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                    content = f.read()
                    
                    # Match asp-controller and asp-action
                    # Sometimes they are on the same line, sometimes not
                    # Easiest way is to find tag by tag
                    tag_pattern = r'<a[^>]+>'
                    form_pattern = r'<form[^>]+>'
                    tags = re.findall(tag_pattern, content) + re.findall(form_pattern, content)
                    for tag in tags:
                        controller_match = re.search(r'asp-controller="([^"]+)"', tag)
                        action_match = re.search(r'asp-action="([^"]+)"', tag)
                        
                        if controller_match and action_match:
                            links.append({
                                'type': 'asp-helper',
                                'controller': controller_match.group(1),
                                'action': action_match.group(1),
                                'file': filepath
                            })
                        elif action_match and not controller_match:
                            # Assume same controller as folder name
                            folder = os.path.basename(os.path.dirname(root)) # Wait, root is usually Views/ControllerName
                            if os.path.basename(root) != 'Shared':
                                links.append({
                                    'type': 'asp-helper-implicit',
                                    'controller': os.path.basename(root),
                                    'action': action_match.group(1),
                                    'file': filepath
                                })
                                
                    href_pattern = r'href="(/?[^"]+)"'
                    actions_pattern = r'action="(/?[^"]+)"'
                    for href in re.findall(href_pattern, content) + re.findall(actions_pattern, content):
                        if href.startswith('#') or href.startswith('http') or href.startswith('mailto:'):
                            continue
                        links.append({
                            'type': 'href',
                            'url': href,
                            'file': filepath
                        })
    return links
    
def scan_controllers_for_redirects():
    redirects = []
    controller_dir = 'Controllers'
    if not os.path.exists(controller_dir):
        return redirects
        
    for filename in os.listdir(controller_dir):
        if filename.endswith('Controller.cs'):
            filepath = os.path.join(controller_dir, filename)
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()
                # RedirectToAction("Action", "Controller")
                pattern1 = r'RedirectToAction\(\s*"([^"]+)"\s*,\s*"([^"]+)"\s*\)'
                # RedirectToAction("Action")
                pattern2 = r'RedirectToAction\(\s*"([^"]+)"\s*\)'
                for act, ctrl in re.findall(pattern1, content):
                    redirects.append({
                        'type': 'redirect',
                        'controller': ctrl,
                        'action': act,
                        'file': filepath
                    })
                current_ctrl = filename.replace('Controller.cs', '')
                for act in re.findall(pattern2, content):
                    redirects.append({
                        'type': 'redirect',
                        'controller': current_ctrl,
                        'action': act,
                        'file': filepath
                    })
    return redirects

def analyze():
    # Ignore identity/default Razor pages if any, but we primarily check MVC
    routes = get_controllers_and_actions()
    links = scan_views_for_links()
    redirects = scan_controllers_for_redirects()
    
    errors = []
    
    # Check ASP links
    for link in links:
        if link['type'] in ('asp-helper', 'asp-helper-implicit'):
            c = link['controller']
            a = link['action']
            if c not in routes:
                errors.append(f"404: Controller '{c}' not found. Referenced in {link['file']} (Action: {a})")
            elif a not in routes[c]:
                # Action methods can be [HttpGet], etc. or overloaded.
                # Also fallback to check if there is an attribute route?
                # This simple script might miss attribute routing, but MVC is usually convention-based
                errors.append(f"404: Action '{a}' not found in Controller '{c}'. Referenced in {link['file']}")
                
        elif link['type'] == 'href':
            href = link['url']
            # simple /Controller/Action
            parts = [p for p in href.split('/') if p and '?' not in p]
            if len(parts) >= 2:
                c, a = parts[0], parts[1]
                # very crude heuristic
                c_capitalized = c.capitalize()
                if c_capitalized in routes and a.capitalize() not in [ac.capitalize() for ac in routes[c_capitalized]]:
                   # errors.append(f"Possible 404: Href {href} -> '{a}' not found in Controller '{c_capitalized}'. Referenced in {link['file']}")
                   pass
            
    for redirect in redirects:
        c = redirect['controller']
        a = redirect['action']
        if c not in routes:
            errors.append(f"404: Redirect Controller '{c}' not found. Referenced in {redirect['file']} (Action: {a})")
        elif a not in set([ac for ac in routes[c]]):
            errors.append(f"404: Redirect Action '{a}' not found in Controller '{c}'. Referenced in {redirect['file']}")

    for error in set(errors):
        print(error)

if __name__ == '__main__':
    analyze()
