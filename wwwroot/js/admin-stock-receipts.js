'use strict';

const receiptForm = document.querySelector('[data-stock-receipt-form]');
if (receiptForm) {
    const lineContainer = receiptForm.querySelector('[data-stock-lines]');
    const rowTemplate = receiptForm.querySelector('[data-stock-row-template]');
    const addRowButton = receiptForm.querySelector('[data-stock-add-row]');
    const bulkInput = receiptForm.querySelector('[data-stock-bulk-input]');
    const bulkLoadButton = receiptForm.querySelector('[data-stock-bulk-load]');
    const bulkError = receiptForm.querySelector('[data-stock-bulk-error]');
    const totalOutput = receiptForm.querySelector('[data-stock-total]');
    if (!lineContainer || !rowTemplate || !addRowButton || !bulkInput || !bulkLoadButton || !bulkError || !totalOutput) {
        throw new Error('The stock receipt form is missing required controls.');
    }

    let nextRowIndex = lineContainer.querySelectorAll('[data-stock-row]').length;
    const formatCurrency = amount => new Intl.NumberFormat('vi-VN', {
        maximumFractionDigits: 2
    }).format(amount);
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
        }
    });

    receiptForm.addEventListener('submit', reindexRows);
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
        updateTotals();
    });

    bulkLoadButton.addEventListener('click', () => {
        bulkError.textContent = '';
        const lines = bulkInput.value.split(/\r?\n/).map(line => line.trim()).filter(Boolean);
        if (lines.length === 0) {
            bulkError.textContent = 'Dán danh sách mã sản phẩm; số lượng; giá nhập trước.';
            return;
        }

        const parsedRows = [];
        const importedProductSlugs = new Set();
        for (let index = 0; index < lines.length; index++) {
            const separator = lines[index].includes('\t') ? '\t' : ';';
            const columns = lines[index].split(separator).map(value => value.trim());
            if (columns.length !== 3) {
                bulkError.textContent = `Dòng ${index + 1}: cần đúng 3 cột mã sản phẩm, số lượng và giá nhập.`;
                return;
            }
            const [productSlug, quantityText, unitCostText] = columns;
            const productOption = [...receiptForm.querySelector('[data-stock-product-template]').options]
                .find(option => option.value.toLocaleLowerCase('vi') === productSlug.toLocaleLowerCase('vi'));
            const quantity = Number(quantityText);
            const normalizedCost = unitCostText.replace(/\s/g, '').replace(',', '.');
            const unitCost = Number(normalizedCost);
            if (!productOption || !Number.isInteger(quantity) || quantity < 1 || quantity > 100000 ||
                !Number.isFinite(unitCost) || unitCost < 1 || unitCost > 9999999999.99 ||
                importedProductSlugs.has(productOption.value.toLocaleLowerCase('vi'))) {
                bulkError.textContent = `Dòng ${index + 1}: mã sản phẩm bị thiếu/trùng hoặc số lượng, giá nhập không hợp lệ.`;
                return;
            }
            importedProductSlugs.add(productOption.value.toLocaleLowerCase('vi'));
            parsedRows.push({ productSlug: productOption.value, quantity: String(quantity), unitCost: String(unitCost) });
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
    });

    updateTotals();
}
