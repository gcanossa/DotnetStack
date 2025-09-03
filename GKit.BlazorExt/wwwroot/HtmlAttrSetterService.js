export function setAttributes(element, attributes, selector) {
    let items = element.querySelectorAll(selector ?? "*");

    items.forEach(item => {
        Object.entries(attributes).forEach(([key, value]) => {
            if(item.dataset.attrSetter === "escape") return;
            
            if (!item.hasAttribute(key) || item.dataset.attrSetter === "force") {
                item.setAttribute(key, value);
            }
        })
    })
}