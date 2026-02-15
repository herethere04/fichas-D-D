document.addEventListener('DOMContentLoaded', () => {
    generateLists();
    generateSpells();
});

function generateLists() {
    // ENGLISH SKILLS to match PDF
    const skills = [
        "Acrobatics (Dex)", "Animal Handling (Wis)", "Arcana (Int)", "Athletics (Str)", 
        "Deception (Cha)", "History (Int)", "Insight (Wis)", "Intimidation (Cha)", 
        "Investigation (Int)", "Medicine (Wis)", "Nature (Int)", "Perception (Wis)", 
        "Performance (Cha)", "Persuasion (Cha)", "Religion (Int)", "Sleight of Hand (Dex)", 
        "Stealth (Dex)", "Survival (Wis)"
    ];

    const stats = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

    // Saving Throws
    const savesDiv = document.getElementById('saves-list');
    stats.forEach((stat, i) => {
        savesDiv.innerHTML += `
            <div class="list-row">
                <input type="checkbox" id="save_check_${i}">
                <input type="text" id="save_val_${i}" class="bonus-input" placeholder="+0">
                <span>${stat}</span>
            </div>
        `;
    });

    // Skills
    const skillsDiv = document.getElementById('skills-list');
    skills.forEach((skill, i) => {
        skillsDiv.innerHTML += `
            <div class="list-row">
                <input type="checkbox" id="skill_check_${i}">
                <input type="text" id="skill_val_${i}" class="bonus-input" placeholder="+0">
                <span>${skill}</span>
            </div>
        `;
    });
}

function generateSpells() {
    // Level 0 (Cantrips) - 8 slots
    const cantripsDiv = document.getElementById('spells-0');
    for(let i=0; i<8; i++) {
        cantripsDiv.innerHTML += `<div class="spell-row"><input type="text" id="spell_0_${i}"></div>`;
    }

    // Levels 1, 2, 3 (Example structure, can be extended to 9)
    [1, 2, 3].forEach(lvl => {
        const div = document.getElementById(`spells-${lvl}`);
        if(div) {
            // Generate 12 spell lines per level
            for(let i=0; i<12; i++) {
                div.innerHTML += `
                    <div class="spell-row">
                        <input type="checkbox" id="spell_prep_${lvl}_${i}">
                        <input type="text" id="spell_${lvl}_${i}">
                    </div>`;
            }
        }
    });
}

// === SAVE & LOAD SYSTEM ===

function saveSheet() {
    const data = {};
    const elements = document.querySelectorAll('input, textarea');
    
    elements.forEach(el => {
        if (el.id) {
            if (el.type === 'checkbox') data[el.id] = el.checked;
            else data[el.id] = el.value;
        }
    });

    const name = document.getElementById('charName').value || 'character';
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name}.json`;
    a.click();
    URL.revokeObjectURL(url);
}

function loadFromFile(input) {
    if (!input.files.length) return;
    const reader = new FileReader();
    reader.onload = (e) => {
        try {
            const data = JSON.parse(e.target.result);
            Object.keys(data).forEach(id => {
                const el = document.getElementById(id);
                if (el) {
                    if (el.type === 'checkbox') el.checked = data[id];
                    else el.value = data[id];
                }
            });
            alert("Sheet loaded successfully!");
        } catch (err) {
            alert("Error loading JSON.");
        }
    };
    reader.readAsText(input.files[0]);
}

// Drag & Drop
document.body.addEventListener('dragover', e => e.preventDefault());
document.body.addEventListener('drop', e => {
    e.preventDefault();
    if (e.dataTransfer.files.length) {
        document.getElementById('file-input').files = e.dataTransfer.files;
        loadFromFile(document.getElementById('file-input'));
    }
});