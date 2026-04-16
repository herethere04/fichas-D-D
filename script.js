// === STATE ===
let currentSheetId = null;
let editMode = false;
let editPassword = null;

document.addEventListener('DOMContentLoaded', () => {
    generateLists();
    generateSpells();
    setupImageUpload();

    // Get sheet ID from URL
    const params = new URLSearchParams(window.location.search);
    currentSheetId = params.get('id');
    const autoEdit = params.get('edit') === '1';

    if (currentSheetId) {
        loadSheetFromAPI(currentSheetId).then(() => {
            if (autoEdit && api.isLoggedIn()) {
                // Newly created sheet - prompt for password to enable editing
                openPasswordModal();
            }
        });
    } else {
        window.location.href = 'fichas.html';
    }

    // Show edit button if logged in
    if (api.isLoggedIn()) {
        document.getElementById('btn-edit-toggle').style.display = '';
    }

    // Password form
    document.getElementById('password-form').addEventListener('submit', async (e) => {
        e.preventDefault();
        const passInput = document.getElementById('edit-pass');
        const errorDiv = document.getElementById('pass-error');
        errorDiv.style.display = 'none';

        try {
            await api.verifyPassword(currentSheetId, passInput.value);
            editPassword = passInput.value;
            enableEditMode();
            closePasswordModal();
            showToast('Edição desbloqueada!', 'success');
        } catch (err) {
            errorDiv.textContent = err.message;
            errorDiv.style.display = 'block';
        }
    });
});

function generateLists() {
    const skills = [
        "Acrobatics (Dex)", "Animal Handling (Wis)", "Arcana (Int)", "Athletics (Str)", 
        "Deception (Cha)", "History (Int)", "Insight (Wis)", "Intimidation (Cha)", 
        "Investigation (Int)", "Medicine (Wis)", "Nature (Int)", "Perception (Wis)", 
        "Performance (Cha)", "Persuasion (Cha)", "Religion (Int)", "Sleight of Hand (Dex)", 
        "Stealth (Dex)", "Survival (Wis)"
    ];

    const stats = ["Strength", "Dexterity", "Constitution", "Intelligence", "Wisdom", "Charisma"];

    const savesDiv = document.getElementById('saves-list');
    stats.forEach((stat, i) => {
        savesDiv.innerHTML += `
            <div class="list-row">
                <input type="checkbox" id="save_check_${i}" disabled>
                <input type="text" id="save_val_${i}" class="bonus-input" placeholder="+0" disabled>
                <span>${stat}</span>
            </div>
        `;
    });

    const skillsDiv = document.getElementById('skills-list');
    skills.forEach((skill, i) => {
        skillsDiv.innerHTML += `
            <div class="list-row">
                <input type="checkbox" id="skill_check_${i}" disabled>
                <input type="text" id="skill_val_${i}" class="bonus-input" placeholder="+0" disabled>
                <span>${skill}</span>
            </div>
        `;
    });
}

function generateSpells() {
    const cantripsDiv = document.getElementById('spells-0');
    for(let i=0; i<8; i++) {
        cantripsDiv.innerHTML += `<div class="spell-row"><input type="text" id="spell_0_${i}" disabled></div>`;
    }

    [1, 2, 3, 4, 5, 6, 7, 8, 9].forEach(lvl => {
        const div = document.getElementById(`spells-${lvl}`);
        if(div) {
            for(let i=0; i<12; i++) {
                div.innerHTML += `
                    <div class="spell-row">
                        <input type="checkbox" id="spell_prep_${lvl}_${i}" disabled>
                        <input type="text" id="spell_${lvl}_${i}" disabled>
                    </div>`;
            }
        }
    });
}

// === API INTEGRATION ===

async function loadSheetFromAPI(id) {
    try {
        const sheet = await api.getSheet(id);
        if (!sheet) return;

        document.getElementById('sheet-title').textContent = sheet.characterName;
        document.title = `${sheet.characterName} - D&D 5e`;

        if (sheet.sheetData && sheet.sheetData !== '{}') {
            const data = typeof sheet.sheetData === 'string' ? JSON.parse(sheet.sheetData) : sheet.sheetData;
            Object.keys(data).forEach(id => {
                const el = document.getElementById(id);
                if (el) {
                    if (el.type === 'checkbox') el.checked = data[id];
                    else el.value = data[id];
                    
                    if (id === 'character_appearance_img' && data[id]) {
                        document.getElementById('character_appearance_img_display').src = data[id];
                        document.getElementById('character_appearance_img_display').style.display = 'block';
                        const label = document.querySelector('#char-img-container .img-label');
                        if (label) label.style.display = 'none';
                    }
                }
            });
        }
    } catch (err) {
        showToast('Erro ao carregar ficha: ' + err.message, 'error');
    }
}

async function saveSheetToAPI() {
    if (!editMode || !editPassword) return;

    const data = {};
    const elements = document.querySelectorAll('#sheet input, #sheet textarea');
    elements.forEach(el => {
        if (el.id) {
            if (el.type === 'checkbox') data[el.id] = el.checked;
            else data[el.id] = el.value;
        }
    });

    const btn = document.getElementById('btn-save');
    btn.disabled = true;
    btn.innerHTML = '<i class="fa-solid fa-spinner fa-spin"></i> Salvando...';

    try {
        await api.updateSheet(currentSheetId, editPassword, JSON.stringify(data));
        showToast('Ficha salva com sucesso!', 'success');
    } catch (err) {
        showToast('Erro ao salvar: ' + err.message, 'error');
    } finally {
        btn.disabled = false;
        btn.innerHTML = '<i class="fa-solid fa-floppy-disk"></i> Salvar';
    }
}

// === EDIT MODE ===

function toggleEditMode() {
    if (editMode) {
        disableEditMode();
    } else {
        openPasswordModal();
    }
}

function enableEditMode() {
    editMode = true;
    document.querySelectorAll('#sheet input, #sheet textarea').forEach(el => {
        el.disabled = false;
    });
    document.getElementById('btn-save').style.display = '';
    const editBtn = document.getElementById('btn-edit-toggle');
    editBtn.innerHTML = '<i class="fa-solid fa-eye"></i> Apenas Visualizar';
    editBtn.classList.add('active');
    document.getElementById('sheet').classList.add('edit-mode');
    
    const imgContainer = document.getElementById('char-img-container');
    if (imgContainer) {
        imgContainer.classList.add('editable');
        document.getElementById('img-instruction').style.display = 'block';
    }
}

function disableEditMode() {
    editMode = false;
    editPassword = null;
    document.querySelectorAll('#sheet input, #sheet textarea').forEach(el => {
        el.disabled = true;
    });
    document.getElementById('btn-save').style.display = 'none';
    const editBtn = document.getElementById('btn-edit-toggle');
    editBtn.innerHTML = '<i class="fa-solid fa-pen"></i> Editar';
    editBtn.classList.remove('active');
    document.getElementById('sheet').classList.remove('edit-mode');

    const imgContainer = document.getElementById('char-img-container');
    if (imgContainer) {
        imgContainer.classList.remove('editable');
        document.getElementById('img-instruction').style.display = 'none';
    }
}

function openPasswordModal() {
    document.getElementById('password-modal').style.display = 'flex';
    document.getElementById('edit-pass').value = '';
    document.getElementById('pass-error').style.display = 'none';
    document.getElementById('edit-pass').focus();
}

function closePasswordModal() {
    document.getElementById('password-modal').style.display = 'none';
}

// Close modal on overlay click
document.getElementById('password-modal')?.addEventListener('click', (e) => {
    if (e.target === e.currentTarget) closePasswordModal();
});

// === TOAST ===

function showToast(message, type = 'info') {
    const toast = document.getElementById('toast');
    toast.textContent = message;
    toast.className = `toast toast-${type}`;
    toast.style.display = 'block';
    setTimeout(() => {
        toast.style.display = 'none';
    }, 3000);
}

// Drag & Drop para importar JSON localmente
document.body.addEventListener('dragover', e => e.preventDefault());
document.body.addEventListener('drop', e => { 
    e.preventDefault(); 
    
    if (!editMode) {
        showToast('Ative a edição primeiro para importar arquivos.', 'warning');
        return;
    }

    const file = e.dataTransfer.files[0];
    if (!file) return;

    if (file.name.endsWith('.json') || file.type === 'application/json') {
        const reader = new FileReader();
        reader.onload = (event) => {
            try {
                const data = JSON.parse(event.target.result);
                Object.keys(data).forEach(id => {
                    const el = document.getElementById(id);
                    if (el) {
                        if (el.type === 'checkbox') {
                            el.checked = data[id];
                        } else {
                            el.value = data[id];
                        }
                    }
                });
                showToast('Ficha importada com sucesso!', 'success');
            } catch (err) {
                showToast('Erro ao ler o arquivo JSON.', 'error');
            }
        };
        reader.readAsText(file);
    } else {
        showToast('Solte apenas arquivos .json válidos.', 'error');
    }
});

// === IMAGE UPLOAD ===
function setupImageUpload() {
    const imgContainer = document.getElementById('char-img-container');
    const imgUpload = document.getElementById('char-img-upload');
    const imgHidden = document.getElementById('character_appearance_img');
    const imgPreview = document.getElementById('character_appearance_img_display');
    const imgLabel = document.querySelector('#char-img-container .img-label');

    if (!imgContainer) return;

    imgContainer.addEventListener('click', () => {
        if (!editMode) return;
        imgUpload.click();
    });

    imgContainer.addEventListener('dragover', e => {
        e.preventDefault();
        if (editMode) imgContainer.style.borderColor = 'var(--gold)';
    });

    imgContainer.addEventListener('dragleave', e => {
        e.preventDefault();
        if (editMode) imgContainer.style.borderColor = '';
    });

    imgContainer.addEventListener('drop', e => {
        e.preventDefault();
        e.stopPropagation(); // Previne de tentar ler como se fosse a ficha json inteira
        if (editMode) imgContainer.style.borderColor = '';
        if (!editMode) return;
        
        const file = e.dataTransfer.files[0];
        if (file) handleImageFile(file);
    });

    imgUpload.addEventListener('change', e => {
        const file = e.target.files[0];
        if (file) handleImageFile(file);
    });

    function handleImageFile(file) {
        if (!file.type.startsWith('image/')) {
            showToast('Por favor, selecione uma imagem válida.', 'error');
            return;
        }
        
        if (file.size > 500 * 1024) {  // 500KB
            showToast('A imagem deve ter no máximo 500KB.', 'error');
            return;
        }

        const reader = new FileReader();
        reader.onload = (e) => {
            const base64 = e.target.result;
            imgHidden.value = base64; // Set value for auto-save
            imgPreview.src = base64;
            imgPreview.style.display = 'block';
            if (imgLabel) imgLabel.style.display = 'none';
        };
        reader.readAsDataURL(file);
    }
}