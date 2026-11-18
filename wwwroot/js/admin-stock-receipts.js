'use strict';

const receiptForm = document.querySelector('[data-stock-receipt-form]');
if (receiptForm) {
    const lineContainer = receiptForm.querySelector('[data-stock-lines]');
    const rowTemplate = receiptForm.querySelector('[data-stock-row-template]');
    const addRowButton = receiptForm.querySelector('[data-stock-add-row]');
    const bulkInput = receiptForm.querySelector('[data-stock-bulk-input]');
    const bulkLoadButton = receiptForm.querySelector('[data-stock-bulk-load]');
    const clearAllButton = receiptForm.querySelector('[data-stock-clear-all]');
    const bulkError = receiptForm.querySelector('[data-stock-bulk-error]');
    const totalOutput = receiptForm.querySelector('[data-stock-total]');
    if (!lineContainer || !rowTemplate || !addRowButton || !bulkInput || !bulkLoadButton || !clearAllButton || !bulkError || !totalOutput) {
        throw new Error('The stock receipt form is missing required controls.');
    }

    let nextRowIndex = lineContainer.querySelectorAll('[data-stock-row]').length;
    const draftStorageKey = 'wstudio-stock-receipt-draft';
    const formatCurrency = amount => new Intl.NumberFormat('vi-VN', {
        maximumFractionDigits: 2
    }).format(amount);
    const saveDraft = () => {
        const draft = {
            partnerId: receiptForm.elements.namedItem('PartnerId')?.value || '',
            receivedOn: receiptForm.elements.namedItem('ReceivedOn')?.value || '',
            notes: receiptForm.elements.namedItem('Notes')?.value || '',
            items: [...lineContainer.querySelectorAll('[data-stock-row]')].map(row => ({
                productSlug: row.querySelector('[data-stock-product]')?.value || '',
                quantity: row.querySelector('[data-stock-quantity]')?.value || '',
                unitCost: row.querySelector('[data-stock-unit-cost]')?.value || ''
            }))
        };
        try {
            sessionStorage.setItem(draftStorageKey, JSON.stringify(draft));
        } catch (error) {
            console.error('Unable to save the stock receipt draft.', error);
            bulkError.textContent = 'Trình duyệt không thể lưu bản nháp tạm thời. Hãy lưu phiếu trước khi rời trang.';
        }
    };
    const reindexRows = () => {
        lineContainer.querySelectorAll('[data-stock-row]').forEach((row, index) => {
            row.querySelectorAll('[name]').forEach(element => {
                element.name = element.name.replace(/Items\[\d+\]/, `Items[${index}]`);
            });
            row.querySelectorAll('[id]').forEach(element => {
                element.id = element.id.replace(/-\d+$/, `-${index}`);
            });
            row.querySelectorAll('label[for]').forEach(label => {
                label.htmlFor = label.htmlFor.replace(/-\d+$/, `-${index}`);
            });
        });
    };

    const updateTotals = () => {
        let total = 0;
        lineContainer.querySelectorAll('[data-stock-row]').forEach(row => {
            const quantity = Number(row.querySelector('[data-stock-quantity]')?.value || 0);
            const unitCost = Number(row.querySelector('[data-stock-unit-cost]')?.value || 0);
            const lineTotal = quantity > 0 && unitCost > 0 ? quantity * unitCost : 0;
            total += lineTotal;
            const output = row.querySelector('[data-stock-line-total]');
            if (output) output.textContent = `${formatCurrency(lineTotal)} ₫`;
        });
        totalOutput.textContent = `${formatCurrency(total)} ₫`;
    };

    const appendRow = (productSlug = '', quantity = '', unitCost = '') => {
        if (lineContainer.querySelectorAll('[data-stock-row]').length >= 200) {
            bulkError.textContent = 'Mỗi phiếu nhập tối đa 200 sản phẩm.';
            return false;
        }
        const index = nextRowIndex++;
        const fragment = rowTemplate.content.cloneNode(true);
        fragment.querySelectorAll('[name], [id], [for]').forEach(element => {
            ['name', 'id', 'for'].forEach(attribute => {
                if (element.hasAttribute(attribute)) {
                    element.setAttribute(attribute, element.getAttribute(attribute).replaceAll('__index__', String(index)));
                }
            });
        });
        const row = fragment.querySelector('[data-stock-row]');
        if (!row) throw new Error('The stock receipt row template is invalid.');
        row.querySelector('[data-stock-product]').value = productSlug;
        row.querySelector('[data-stock-quantity]').value = quantity;
        row.querySelector('[data-stock-unit-cost]').value = unitCost;
        lineContainer.append(fragment);
        return true;
    };

    addRowButton.addEventListener('click', () => {
        bulkError.textContent = '';
        if (appendRow()) {
            lineContainer.lastElementChild?.querySelector('[data-stock-product]')?.focus();
            saveDraft();
        }
    });

    clearAllButton.addEventListener('click', () => {
        lineContainer.replaceChildren();
        nextRowIndex = 0;
        appendRow();
        bulkInput.value = '';
        try {
            sessionStorage.removeItem(draftStorageKey);
        } catch (error) {
            console.error('Unable to clear the stock receipt draft.', error);
        }
        bulkError.textContent = 'Đã xóa danh sách sản phẩm khỏi phiếu.';
        updateTotals();
    });

    receiptForm.addEventListener('submit', () => {
        reindexRows();
        saveDraft();
    });
    receiptForm.addEventListener('input', saveDraft);
    receiptForm.addEventListener('change', saveDraft);
    lineContainer.addEventListener('input', updateTotals);
    lineContainer.addEventListener('change', updateTotals);
    lineContainer.addEventListener('click', event => {
        const removeButton = event.target.closest('[data-stock-remove-row]');
        if (!removeButton) return;
        const rows = lineContainer.querySelectorAll('[data-stock-row]');
        if (rows.length === 1) {
            rows[0].querySelector('[data-stock-product]').value = '';
            rows[0].querySelector('[data-stock-quantity]').value = '';
            rows[0].querySelector('[data-stock-unit-cost]').value = '';
        } else {
            removeButton.closest('[data-stock-row]').remove();
        }
        reindexRows();
        updateTotals();
        saveDraft();
    });

    bulkLoadButton.addEventListener('click', () => {
        bulkError.textContent = '';
        const lines = bulkInput.value.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
        if (lines.length === 0) {
            bulkError.textContent = 'Dán danh sách mã sản phẩm; số lượng; giá nhập trước.';
            return;
        }

        const parseCost = rawValue => {
            let value = rawValue.replace(/[₫đ]/gi, '').replace(/vnd/gi, '').replace(/[\s\u00a0]/g, '');
            const lastComma = value.lastIndexOf(',');
            const lastDot = value.lastIndexOf('.');
            if (lastComma >= 0 && lastDot >= 0) {
                const decimalSeparator = lastComma > lastDot ? ',' : '.';
                const groupingSeparator = decimalSeparator === ',' ? '.' : ',';
                value = value.split(groupingSeparator).join('');
                if (decimalSeparator === ',') value = value.replace(',', '.');
            } else {
                const separator = lastComma >= 0 ? ',' : lastDot >= 0 ? '.' : '';
                if (separator) {
                    const parts = value.split(separator);
                    const lastPart = parts[parts.length - 1];
                    if (parts.length === 2 && lastPart.length > 0 && lastPart.length <= 2) value = `${parts[0]}.${lastPart}`;
                    else value = parts.join('');
                }
            }
            return /^\d+(\.\d{1,2})?$/.test(value) ? Number(value) : Number.NaN;
        };
        const productSelectTemplate = receiptForm.querySelector('[data-stock-product-template]');
        if (!productSelectTemplate) throw new Error('The stock receipt product list is missing.');
        const parsedRows = [];
        for (let index = 0; index < lines.length; index++) {
            const separator = lines[index].includes('\t') ? '\t' : ';';
            const columns = lines[index].split(separator).map(value => value.trim());
            const headerText = columns.join(' ').toLocaleLowerCase('vi');
            if (index === 0 && columns.length === 3 && /(mã|sku|code|sản phẩm)/.test(headerText) && /(số lượng|quantity|qty|\bsl\b)/.test(headerText)) continue;
            if (columns.length !== 3) {
                bulkError.textContent = `Dòng ${index + 1}: cần đúng 3 cột mã sản phẩm, số lượng và giá nhập.`;
                return;
            }
            const [productSlug, quantityText, unitCostText] = columns;
            const productOption = [...productSelectTemplate.options]
                .find(option => option.value.toLocaleLowerCase('vi') === productSlug.toLocaleLowerCase('vi'));
            const quantity = Number(quantityText);
            const unitCost = parseCost(unitCostText);
            if (!productOption || !Number.isInteger(quantity) || quantity < 1 || quantity > 100000 ||
                !Number.isFinite(unitCost) || unitCost < 1 || unitCost > 9999999999.99) {
                bulkError.textContent = `Dòng ${index + 1}: mã sản phẩm chưa có trong danh mục hoặc số lượng/giá nhập không hợp lệ.`;
                return;
            }
            parsedRows.push({ productSlug: productOption.value, quantity: String(quantity), unitCost: String(unitCost) });
        }

        if (parsedRows.length === 0) {
            bulkError.textContent = 'Không tìm thấy dòng sản phẩm hợp lệ trong nội dung đã dán.';
            return;
        }
        if (parsedRows.length > 200) {
            bulkError.textContent = 'Mỗi phiếu nhập tối đa 200 sản phẩm.';
            return;
        }
        lineContainer.replaceChildren();
        nextRowIndex = 0;
        parsedRows.forEach(row => appendRow(row.productSlug, row.quantity, row.unitCost));
        bulkInput.value = '';
        bulkError.textContent = `Đã nạp ${parsedRows.length} sản phẩm. Kiểm tra danh sách rồi lưu phiếu.`;
        updateTotals();
        saveDraft();
    });

    let draftText = '';
    try {
        if (receiptForm.dataset.stockReceiptSaved === 'true') {
            sessionStorage.removeItem(draftStorageKey);
        } else {
            draftText = sessionStorage.getItem(draftStorageKey) || '';
        }
    } catch (error) {
        console.error('Unable to access the stock receipt draft.', error);
    }
    if (draftText) {
        try {
            const draft = JSON.parse(draftText);
            const draftItems = draft.items || draft.Items || [];
            if (draftItems.length > 0) {
                lineContainer.replaceChildren();
                nextRowIndex = 0;
                draftItems.forEach(item => appendRow(
                    item.productSlug || item.ProductSlug || '',
                    String(item.quantity ?? item.Quantity ?? ''),
                    String(item.unitCost ?? item.UnitCost ?? '')));
            }
            const partnerSelect = receiptForm.elements.namedItem('PartnerId');
            const dateInput = receiptForm.elements.namedItem('ReceivedOn');
            const notesInput = receiptForm.elements.namedItem('Notes');
            if (partnerSelect) partnerSelect.value = draft.partnerId || draft.PartnerId || '';
            if (dateInput && (draft.receivedOn || draft.ReceivedOn)) dateInput.value = draft.receivedOn || draft.ReceivedOn;
            if (notesInput) notesInput.value = draft.notes || draft.Notes || '';
            bulkError.textContent = 'Phiếu nhập chưa lưu được. Dữ liệu đã được khôi phục để bạn sửa và gửi lại.';
        } catch (error) {
            console.error('Unable to restore the stock receipt draft.', error);
            bulkError.textContent = 'Không thể khôi phục danh sách nhập hàng. Hãy tải lại trang trước khi nhập lại.';
            sessionStorage.removeItem(draftStorageKey);
        }
    }

    updateTotals();
}
