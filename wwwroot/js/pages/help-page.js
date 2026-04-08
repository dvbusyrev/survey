function handleSelectChange(select) {
    const role = select.value;
    if (!role) {
        return;
    }

    loadAndUploadInstruction(role);
    select.value = '';
}

function loadAndUploadInstruction(role) {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.docx';
    input.addEventListener('change', async function (event) {
        const file = event.target.files?.[0];
        if (!file) {
            return;
        }

        const formData = new FormData();
        formData.append('file', file);
        formData.append('role', role);

        try {
            const response = await fetch('/help/upload', {
                method: 'POST',
                body: formData
            });

            if (!response.ok) {
                throw new Error('Ошибка загрузки файла');
            }

            alert('Файл успешно загружен');
        } catch (error) {
            alert(error instanceof Error ? error.message : 'Ошибка загрузки файла');
        }
    });

    input.click();
}
