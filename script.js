// CONFIGURAÇÃO DE DADOS DO SISTEMA D&D 5E
const skillsData = [
    { name: "Acrobacia", stat: "dex" },
    { name: "Adestrar Animais", stat: "wis" },
    { name: "Arcanismo", stat: "int" },
    { name: "Atletismo", stat: "str" },
    { name: "Atuação", stat: "cha" },
    { name: "Enganação", stat: "cha" },
    { name: "Furtividade", stat: "dex" },
    { name: "História", stat: "int" },
    { name: "Intimidação", stat: "cha" },
    { name: "Intuição", stat: "wis" },
    { name: "Investigação", stat: "int" },
    { name: "Medicina", stat: "wis" },
    { name: "Natureza", stat: "int" },
    { name: "Percepção", stat: "wis" },
    { name: "Persuasão", stat: "cha" },
    { name: "Prestidigitação", stat: "dex" },
    { name: "Religião", stat: "int" },
    { name: "Sobrevivência", stat: "wis" }
];

const stats = ['str', 'dex', 'con', 'int', 'wis', 'cha'];

// INICIALIZAÇÃO
document.addEventListener('DOMContentLoaded', () => {
    generateSkillsHTML();
    generateSavesHTML();
    updateModifiers(); // Calcula tudo na largada
});

// --- GERADORES DE HTML ---

function generateSkillsHTML() {
    const container = document.getElementById('skills-list');
    let html = '';
    skillsData.forEach((skill, index) => {
        // IDs únicos para cada checkbox e span de valor
        html += `
        <div class="skill-row">
            <input type="checkbox" id="skill_prof_${index}" onchange="updateModifiers()">
            <label for="skill_prof_${index}">${skill.name} <span style="font-size:0.7em; opacity:0.6">(${skill.stat.toUpperCase()})</span></label>
            <span id="skill_val_${index}" class="skill-val">+0</span>
        </div>`;
    });
    container.innerHTML = html;
}

function generateSavesHTML() {
    const container = document.getElementById('saving-throws-list');
    let html = '';
    stats.forEach(stat => {
        html += `
        <div class="skill-row">
            <input type="checkbox" id="save_prof_${stat}" onchange="updateModifiers()">
            <label for="save_prof_${stat}">${stat.toUpperCase()}</label>
            <span id="save_val_${stat}" class="skill-val">+0</span>
        </div>`;
    });
    container.innerHTML = html;
}

// --- LÓGICA DE CÁLCULO ---

function getMod(score) {
    return Math.floor((score - 10) / 2);
}

function updateModifiers() {
    const profBonus = parseInt(document.getElementById('profBonus').value) || 2;
    const mods = {};

    // 1. Atualiza Modificadores de Atributos
    stats.forEach(stat => {
        const score = parseInt(document.getElementById(stat).value) || 10;
        const mod = getMod(score);
        mods[stat] = mod;
        
        // Atualiza visual do modificador grande
        const sign = mod >= 0 ? '+' : '';
        document.getElementById(`${stat}-mod`).innerText = `${sign}${mod}`;

        // 2. Atualiza Salvaguardas
        const isProf = document.getElementById(`save_prof_${stat}`).checked;
        const totalSave = mod + (isProf ? profBonus : 0);
        const saveSign = totalSave >= 0 ? '+' : '';
        document.getElementById(`save_val_${stat}`).innerText = `${saveSign}${totalSave}`;
    });

    // 3. Atualiza Perícias
    skillsData.forEach((skill, index) => {
        const isProf = document.getElementById(`skill_prof_${index}`).checked;
        const attrMod = mods[skill.stat];
        // Lógica simples: Proficiência (Sim/Não). 
        // Nota: Expertise exigiria checkboxes triplos, mantive simples para D&D 5e padrão.
        const total = attrMod + (isProf ? profBonus : 0);
        const sign = total >= 0 ? '+' : '';
        document.getElementById(`skill_val_${index}`).innerText = `${sign}${total}`;
    });

    // 4. Percepção Passiva (10 + Mod Wis + (Prof se tiver em Percepção))
    // Acha o index da percepção
    const percIndex = skillsData.findIndex(s => s.name === "Percepção");
    const percIsProf = document.getElementById(`skill_prof_${percIndex}`).checked;
    const passive = 10 + mods['wis'] + (percIsProf ? profBonus : 0);
    document.getElementById('passiveWis').innerText = passive;
}

// --- ARQUIVO E NAVEGAÇÃO ---

function newSheet() {
    document.getElementById('start-screen').classList.add('hidden');
    document.getElementById('app').classList.remove('hidden');
    // Limpar campos se necessário, ou deixar padrão
}

function closeSheet() {
    if(confirm("Deseja sair? Alterações não salvas serão perdidas.")) {
        document.getElementById('app').classList.add('hidden');
        document.getElementById('start-screen').classList.remove('hidden');
    }
}

function getAllInputIds() {
    // Coleta todos os inputs, textareas e selects
    const inputs = document.querySelectorAll('input, textarea');
    const ids = [];
    inputs.forEach(el => {
        if (el.id) ids.push(el.id);
    });
    return ids;
}

function saveSheet() {
    const data = {};
    const ids = getAllInputIds();
    
    ids.forEach(id => {
        const el = document.getElementById(id);
        if (el.type === 'checkbox') {
            data[id] = el.checked;
        } else {
            data[id] = el.value;
        }
    });

    const name = data.charName || "Personagem";
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `${name}.json`;
    a.click();
    URL.revokeObjectURL(url);
}

function handleFile(file) {
    const reader = new FileReader();
    reader.onload = (e) => {
        try {
            const data = JSON.parse(e.target.result);
            loadDataIntoForm(data);
        } catch (err) {
            alert("Erro ao ler JSON. O arquivo está corrompido?");
            console.error(err);
        }
    };
    reader.readAsText(file);
}

function loadDataIntoForm(data) {
    // 1. Abre a tela
    newSheet();
    
    // 2. Popula
    Object.keys(data).forEach(key => {
        const el = document.getElementById(key);
        if (el) {
            if (el.type === 'checkbox') {
                el.checked = data[key];
            } else {
                el.value = data[key];
            }
        }
    });

    // 3. Recalcula tudo
    updateModifiers();
}

function loadFromFile(input) {
    if(input.files.length) handleFile(input.files[0]);
}

// Drag and Drop Events
const dropZone = document.getElementById('drop-zone');
dropZone.addEventListener('dragover', (e) => { e.preventDefault(); dropZone.style.borderColor = '#c5a059'; });
dropZone.addEventListener('dragleave', (e) => { e.preventDefault(); dropZone.style.borderColor = '#a0a0a0'; });
dropZone.addEventListener('drop', (e) => {
    e.preventDefault();
    dropZone.style.borderColor = '#a0a0a0';
    if(e.dataTransfer.files.length) handleFile(e.dataTransfer.files[0]);
});