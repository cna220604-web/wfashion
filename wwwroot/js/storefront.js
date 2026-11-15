const storefrontConfig = JSON.parse(document.getElementById('storefront-data').textContent);
const products = storefrontConfig.products;
const isCustomer = storefrontConfig.isCustomer;
const isAuthenticated = storefrontConfig.isAuthenticated;
const money = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 });
const tabSessionId = (() => {
    const storageKey = 'studio.tab-session-id';
    let id = sessionStorage.getItem(storageKey);
    if (!id) {
        id = crypto.randomUUID();
        sessionStorage.setItem(storageKey, id);
    }
    return id;
})();
const readStore = (key, fallback) => {
    try {
        const value = JSON.parse(sessionStorage.getItem(key));
        return value ?? fallback;
    } catch {
        return fallback;
    }
};
const saveStore = (key, value) => {
    try {
        sessionStorage.setItem(key, JSON.stringify(value));
        return true;
    } catch {
        showToast('Không thể lưu dữ liệu cho tab này.');
        return false;
    }
};
const savedCart = readStore('studio.cart', []);
const savedFavorites = readStore('studio.favorites', []);
let cart = (Array.isArray(savedCart) ? savedCart : []).filter(item => item && products.some(product => product.id === item.id) && typeof item.size === 'string' && Number.isInteger(item.quantity) && item.quantity > 0 && item.quantity <= 10);
let favorites = new Set((Array.isArray(savedFavorites) ? savedFavorites : []).filter(id => products.some(product => product.id === id)));
let selectedCategory = 'all';
let favoritesOnly = false;
let currentProductPage = 1;
const productsPerPage = 12;
let selectedProduct = null;
let selectedSize = null;
let resumedPendingPurchase = null;
const productHistory = [];
let scrollRevealObserver = null;
const savedAiChatHistory = readStore('studio.ai-chat', []);
let chatHistory = Array.isArray(savedAiChatHistory)
    ? savedAiChatHistory.filter(message => message && (message.role === 'user' || message.role === 'assistant') && typeof message.content === 'string').map(message => ({
        role: message.role,
        content: message.content,
        productCodes: Array.isArray(message.productCodes)
            ? message.productCodes.filter(code => typeof code === 'string' && products.some(product => product.id === code))
            : []
    }))
    : [];
let supportConversationId = null;
let supportAfterMessageId = 0;
let supportPollTimer = null;
let supportLoaded = false;
let toastTimer;

const saveCart = () => saveStore('studio.cart', cart);
const escapeHtml = value => String(value).replace(/[&<>"']/g, character => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[character]);
const imageUrl = (productId, imageId) => imageId.startsWith('uploads/')
    ? `/images/${imageId.split('/').map(encodeURIComponent).join('/')}`
    : /^look-(?:0[1-9]|[12]\d|30)$/.test(productId)
        ? `/images/products/${productId}.jpg`
        : `https://images.unsplash.com/${encodeURIComponent(imageId)}?auto=format&fit=crop&w=800&q=82`;
const showToast = message => {
    const toast = document.getElementById('store-toast');
    toast.textContent = message;
    toast.hidden = false;
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => { toast.hidden = true; }, 2500);
};
const getStoreReturnUrl = () => {
    const sectionIds = ['color-stylist', 'season-studio', 'collection', 'home'];
    const viewportAnchor = window.innerHeight * 0.35;
    const visibleSection = sectionIds
        .map(id => document.getElementById(id))
        .filter(Boolean)
        .map(section => ({ section, bounds: section.getBoundingClientRect() }))
        .filter(({ bounds }) => bounds.top <= viewportAnchor && bounds.bottom > viewportAnchor)
        .sort((a, b) => Math.abs(a.bounds.top - viewportAnchor) - Math.abs(b.bounds.top - viewportAnchor))[0]?.section;
    const currentHash = window.location.hash;
    const section = visibleSection
        || (currentHash ? document.getElementById(currentHash.slice(1)) : null)
        || document.getElementById('collection');
    return `${window.location.pathname}${window.location.search}#${section?.id || 'collection'}`;
};
const redirectToLoginFromStore = () => {
    window.location.href = '/Account/Login?returnUrl=' + encodeURIComponent(getStoreReturnUrl());
};
const saveChatHistory = () => saveStore('studio.ai-chat', chatHistory);
const normalizeCatalogText = value => String(value).normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/gi, 'd').toLocaleLowerCase('vi');
const initializeCatalogFilters = () => {
    const colorFilter = document.getElementById('color-filter');
    const sizeFilter = document.getElementById('size-filter');
    [...new Set(products.map(product => product.color).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'vi')).forEach(color => {
        const option = document.createElement('option');
        option.value = color;
        option.textContent = color;
        colorFilter.append(option);
    });
    [...new Set(products.flatMap(product => product.sizes).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'vi')).forEach(size => {
        const option = document.createElement('option');
        option.value = size;
        option.textContent = size;
        sizeFilter.append(option);
    });
};
const resetProductPage = () => { currentProductPage = 1; };
const renderProducts = () => {
    const query = normalizeCatalogText(document.getElementById('search-input').value.trim());
    const color = document.getElementById('color-filter').value;
    const size = document.getElementById('size-filter').value;
    const priceRange = document.getElementById('price-filter').value;
    const [minimumPrice, maximumPrice] = priceRange ? priceRange.split('-').map(value => value ? Number(value) : null) : [null, null];
    const sort = document.getElementById('sort-select').value;
    let visible = products.filter(product => {
        const matchesText = normalizeCatalogText(`${product.name} ${product.id} ${product.categoryLabel} ${product.color}`).includes(query);
        const matchesPrice = (minimumPrice === null || product.price >= minimumPrice) && (maximumPrice === null || product.price < maximumPrice);
        return (selectedCategory === 'all' || product.category === selectedCategory)
            && (!favoritesOnly || favorites.has(product.id))
            && (!color || product.color === color)
            && (!size || product.sizes.includes(size))
            && matchesPrice
            && matchesText;
    });
    if (sort === 'price-asc') visible.sort((a, b) => a.price - b.price);
    if (sort === 'price-desc') visible.sort((a, b) => b.price - a.price);
    const totalPages = Math.max(1, Math.ceil(visible.length / productsPerPage));
    currentProductPage = Math.min(currentProductPage, totalPages);
    const pageProducts = visible.slice((currentProductPage - 1) * productsPerPage, currentProductPage * productsPerPage);
    document.getElementById('product-grid').innerHTML = pageProducts.map(product => `
        <article class="product-card" draggable="true" data-look-drag="${escapeHtml(product.id)}">
            <button class="product-image-button" type="button" data-product="${escapeHtml(product.id)}" aria-label="Xem ${escapeHtml(product.name)}">
                <img src="${imageUrl(product.id, product.image)}" alt="${escapeHtml(product.name)}" loading="lazy" />
                <span class="product-badge">${escapeHtml(product.categoryLabel)}</span>
            </button>
            <button class="product-heart ${favorites.has(product.id) ? 'is-favorite' : ''}" type="button" data-favorite="${escapeHtml(product.id)}" aria-label="${favorites.has(product.id) ? 'Bỏ khỏi' : 'Thêm vào'} yêu thích" aria-pressed="${favorites.has(product.id)}">${favorites.has(product.id) ? '♥' : '♡'}</button>
            <div class="product-meta"><button class="product-name" type="button" data-product="${escapeHtml(product.id)}">${escapeHtml(product.name)}</button><span>${escapeHtml(product.color)}</span><div class="product-rating" aria-label="${product.review.reviewCount ? `Đánh giá ${product.review.averageRating.toFixed(1)} trên 5 từ ${product.review.reviewCount} lượt` : 'Chưa có đánh giá'}">${product.review.reviewCount ? `★ ${product.review.averageRating.toFixed(1)} <span>(${product.review.reviewCount})</span>` : '<span>Chưa có đánh giá</span>'}</div></div>
            <div class="product-price-row"><strong>${money.format(product.price)}</strong><button class="add-product" type="button" data-product="${escapeHtml(product.id)}" ${product.stock < 1 ? 'disabled' : ''}>${product.stock < 1 ? 'HẾT HÀNG' : 'CHỌN MÓN'} <span aria-hidden="true">${product.stock < 1 ? '' : '＋'}</span></button></div>
        </article>`).join('');
    document.getElementById('empty-products').hidden = visible.length > 0;
    document.getElementById('product-results-count').textContent = `${visible.length} sản phẩm · Trang ${currentProductPage}/${totalPages}`;
    const pagination = document.getElementById('product-pagination');
    pagination.hidden = visible.length <= productsPerPage;
    document.getElementById('products-page-label').textContent = `Trang ${currentProductPage} / ${totalPages}`;
    document.getElementById('products-previous').disabled = currentProductPage === 1;
    document.getElementById('products-next').disabled = currentProductPage === totalPages;
};
const renderCart = () => {
    const count = cart.reduce((total, item) => total + item.quantity, 0);
    document.getElementById('cart-count').textContent = count;
    document.getElementById('drawer-count').textContent = count ? `(${count})` : '';
    document.getElementById('cart-empty').hidden = count !== 0;
    document.getElementById('cart-summary').hidden = count === 0;
    const itemsHost = document.getElementById('cart-items');
    itemsHost.innerHTML = cart.map((item, index) => {
        const product = products.find(candidate => candidate.id === item.id);
        return `<article class="cart-item"><img src="${imageUrl(product.id, product.image)}" alt="" /><div class="cart-item-info"><strong>${escapeHtml(product.name)}</strong><span>Size: ${escapeHtml(item.size)}</span><b>${money.format(product.price)}</b><div class="quantity-control"><button type="button" data-quantity="${index}" data-delta="-1" aria-label="Giảm số lượng">−</button><span>${item.quantity}</span><button type="button" data-quantity="${index}" data-delta="1" aria-label="Tăng số lượng">＋</button><button type="button" class="remove-item" data-remove="${index}">Xóa</button></div></div></article>`;
    }).join('');
    const subtotal = cart.reduce((total, item) => total + products.find(product => product.id === item.id).price * item.quantity, 0);
    const shipping = subtotal === 0 || subtotal >= 800000 ? 0 : 30000;
    document.getElementById('subtotal-value').textContent = money.format(subtotal);
    document.getElementById('shipping-value').textContent = shipping ? money.format(shipping) : 'Miễn phí';
    document.getElementById('total-value').textContent = money.format(subtotal + shipping);
    document.getElementById('checkout-total').textContent = money.format(subtotal + shipping);
};
const colorPalettes = {
    black: { label: 'Đen', hex: '#282625', matches: ['white', 'beige', 'wine', 'pink'], tip: 'Đen là gam nền linh hoạt. Trắng tạo tương phản gọn gàng; be làm tổng thể mềm hơn; đỏ rượu hoặc hồng tạo điểm nhấn.' },
    white: { label: 'Trắng ngà', hex: '#f1eadc', matches: ['navy', 'beige', 'brown', 'lavender'], tip: 'Trắng ngà hợp với xanh navy để trông chỉn chu, be và nâu để ấm áp, hoặc lavender để dịu dàng.' },
    beige: { label: 'Be cát', hex: '#cbb89c', matches: ['white', 'brown', 'olive', 'rust'], tip: 'Be cát phối đẹp với trắng ngà, nâu cacao hoặc xanh olive; cam đất đem lại nét ấm áp.' },
    brown: { label: 'Nâu cacao', hex: '#705044', matches: ['beige', 'white', 'blue', 'mustard'], tip: 'Nâu cacao đi cùng be hoặc trắng ngà tạo vẻ thanh lịch; xanh denim hay vàng mù tạt tạo điểm nhấn.' },
    navy: { label: 'Xanh navy', hex: '#26394b', matches: ['white', 'beige', 'sky', 'coral'], tip: 'Navy kết hợp trắng ngà rất dễ mặc; xanh da trời tạo lớp màu đồng điệu, san hô làm tổng thể tươi hơn.' },
    blue: { label: 'Xanh denim', hex: '#66849a', matches: ['white', 'beige', 'brown', 'mustard'], tip: 'Xanh denim hợp với trắng ngà, be và nâu ấm. Vàng mù tạt tạo tương phản vui mắt.' },
    olive: { label: 'Xanh olive', hex: '#73734f', matches: ['beige', 'white', 'rust', 'pink'], tip: 'Olive là gam trầm tự nhiên; phối với be, trắng ngà hoặc cam đất hài hòa. Hồng phấn tạo nét mềm mại.' },
    wine: { label: 'Đỏ rượu', hex: '#773c43', matches: ['beige', 'black', 'pink', 'gray'], tip: 'Đỏ rượu nổi bật hơn khi đi cùng be; đen hoặc xám giúp cân bằng, hồng phấn tạo sắc độ lãng mạn.' },
    gray: { label: 'Xám đá', hex: '#888987', matches: ['white', 'navy', 'wine', 'lime'], tip: 'Xám đá dễ cân bằng với trắng và navy; đỏ rượu hoặc xanh lime tạo điểm nhấn cá tính.' },
    pink: { label: 'Hồng phấn', hex: '#d9aeb1', matches: ['white', 'beige', 'navy', 'olive'], tip: 'Hồng phấn đi cùng trắng hoặc be tạo cảm giác dịu nhẹ; navy và olive giúp bản phối rõ nét hơn.' },
    red: { label: 'Đỏ tươi', hex: '#c7463d', matches: ['white', 'black', 'navy', 'pink'], tip: 'Đỏ tươi nổi bật khi đi cùng trắng, đen hoặc navy. Hồng tạo bản phối thời trang theo sắc độ.' },
    orange: { label: 'Cam', hex: '#e78343', matches: ['navy', 'white', 'brown', 'teal'], tip: 'Cam rực rỡ được cân bằng bởi navy, trắng hoặc nâu; xanh teal tạo cặp màu tương phản thú vị.' },
    rust: { label: 'Cam đất', hex: '#b76443', matches: ['beige', 'olive', 'navy', 'cream'], tip: 'Cam đất hợp với be và olive theo bảng màu thiên nhiên; navy tạo tương phản đậm chất thu đông.' },
    yellow: { label: 'Vàng nắng', hex: '#edc84b', matches: ['navy', 'blue', 'white', 'gray'], tip: 'Vàng nắng đi cùng navy hoặc denim cho vẻ tươi trẻ; trắng và xám làm dịu sắc vàng.' },
    mustard: { label: 'Vàng mù tạt', hex: '#c49a32', matches: ['navy', 'brown', 'olive', 'cream'], tip: 'Vàng mù tạt hợp với navy, nâu và olive; phối với kem giúp tổng thể nhẹ nhàng hơn.' },
    mint: { label: 'Xanh bạc hà', hex: '#9ccbb8', matches: ['white', 'beige', 'pink', 'brown'], tip: 'Xanh bạc hà dịu mắt khi kết hợp trắng, be hoặc nâu; hồng phấn tạo cảm giác ngọt ngào.' },
    teal: { label: 'Xanh teal', hex: '#397e7a', matches: ['orange', 'cream', 'brown', 'coral'], tip: 'Teal có thể phối với cam để tương phản, hoặc kem và nâu để trầm ấm.' },
    forest: { label: 'Xanh lá đậm', hex: '#365a43', matches: ['cream', 'beige', 'pink', 'mustard'], tip: 'Xanh lá đậm phối đẹp cùng kem, be và vàng mù tạt; hồng phấn tạo điểm nhấn bất ngờ.' },
    lime: { label: 'Xanh lime', hex: '#a8b94a', matches: ['navy', 'gray', 'white', 'purple'], tip: 'Xanh lime nổi bật trên nền navy hoặc xám; tím tạo cặp màu tương phản cá tính.' },
    sky: { label: 'Xanh da trời', hex: '#91b9d1', matches: ['white', 'navy', 'beige', 'coral'], tip: 'Xanh da trời dễ phối cùng trắng và navy; be làm dịu, san hô thêm chút sinh động.' },
    purple: { label: 'Tím', hex: '#77558f', matches: ['white', 'gray', 'yellow', 'pink'], tip: 'Tím hài hòa với trắng hoặc xám; vàng tạo tương phản, hồng cho cảm giác mềm mại.' },
    lavender: { label: 'Tím lavender', hex: '#b7a5cd', matches: ['white', 'beige', 'gray', 'mint'], tip: 'Lavender dịu nhẹ cùng trắng, be hoặc xám; xanh bạc hà tạo bảng màu pastel.' },
    coral: { label: 'San hô', hex: '#e88778', matches: ['navy', 'teal', 'white', 'beige'], tip: 'San hô nổi bật trên nền navy hoặc teal; trắng và be giúp bản phối tươi sáng, dễ mặc.' },
    cream: { label: 'Kem', hex: '#e8dcc5', matches: ['rust', 'forest', 'mustard', 'teal'], tip: 'Màu kem là nền dịu cho cam đất, xanh lá đậm, vàng mù tạt và teal.' }
};
const colorAliases = {
    black: ['den', 'black'], white: ['trang', 'ivory', 'white'], cream: ['kem', 'cream', 'canvas tu nhien'],
    beige: ['be', 'cat', 'linen'], brown: ['nau', 'cacao', 'chester', 'sang'],
    navy: ['navy'], blue: ['xanh denim', 'denim', 'xanh duong', 'blue'], sky: ['xanh nhat', 'xanh da troi', 'sky'],
    olive: ['olive'], forest: ['xanh la dam', 'xanh luc', 'xanh reu', 'forest'], teal: ['teal', 'xanh ngoc'],
    wine: ['do ruou', 'wine'], red: ['do tuoi', 'red'], rust: ['cam dat', 'terracotta', 'rust'], orange: ['cam', 'orange'],
    mustard: ['mu tat', 'mustard'], yellow: ['vang', 'yellow'], mint: ['bac ha', 'mint'],
    gray: ['xam', 'ghi', 'gray'], pink: ['hong', 'pink'], purple: ['tim', 'purple'], lavender: ['lavender'], coral: ['san ho', 'coral'], lime: ['lime']
};
const colorKeyFor = value => {
    const normalized = normalizeCatalogText(value);
    return Object.entries(colorAliases).find(([, aliases]) => aliases.some(alias => normalized.includes(alias)))?.[0] || null;
};
const seasonalGuides = {
    spring: { name: 'mùa xuân', description: 'Sắc màu tươi sáng và những thiết kế nhẹ nhàng cho ngày giao mùa.', keywords: ['ao so mi', 'vay', 'cardigan', 'ao khoac nhe', 'hoa', 'pastel'], colors: ['hong', 'xanh la', 'kem', 'vang', 'trang'] },
    summer: { name: 'mùa hè', description: 'Ưu tiên những thiết kế thoáng nhẹ, linh hoạt cho ngày nắng.', keywords: ['ao thun', 'ao ngan tay', 'short', 'vay', 'linen', 'cotton', 'voan'], colors: ['trang', 'xanh', 'kem', 'vàng', 'be'] },
    autumn: { name: 'mùa thu', description: 'Gợi ý phối lớp nhẹ cùng những gam màu ấm và trầm hơn.', keywords: ['ao khoac', 'blazer', 'cardigan', 'len', 'quan dai', 'sweater'], colors: ['nau', 'olive', 'be', 'wine', 'kem'] },
    winter: { name: 'mùa đông', description: 'Chọn thiết kế có thể phối nhiều lớp và bảng màu ấm áp.', keywords: ['ao khoac', 'len', 'cardigan', 'sweater', 'coat', 'hoodie', 'khan'], colors: ['den', 'xam', 'navy', 'nau', 'wine'] }
};
const getCurrentSeason = () => {
    const month = new Date().getMonth();
    if (month >= 2 && month <= 4) return 'spring';
    if (month >= 5 && month <= 7) return 'summer';
    if (month >= 8 && month <= 10) return 'autumn';
    return 'winter';
};
let activeSeason = getCurrentSeason();
let seasonTextTransitionId = 0;
const replaySeasonTextReveal = () => {
    if (!scrollRevealObserver || window.matchMedia('(prefers-reduced-motion: reduce)').matches) return;

    document.querySelectorAll('#season-studio .scroll-reveal').forEach(element => {
        element.classList.remove('is-scroll-visible');
        const bounds = element.getBoundingClientRect();
        const isVisible = bounds.bottom > 0 && bounds.top < window.innerHeight;
        if (isVisible) requestAnimationFrame(() => element.classList.add('is-scroll-visible'));
    });
};
const renderSeason = season => {
    const guide = seasonalGuides[season];
    if (!guide) return;
    const shouldAnimateSeasonCopy = activeSeason !== season && !window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const transitionId = ++seasonTextTransitionId;
    const seasonName = document.getElementById('season-name');
    const seasonDescription = document.getElementById('season-description');
    const recommendationTitle = document.getElementById('season-recommendation-title');
    const seasonCopy = [seasonName, seasonDescription, recommendationTitle];
    const updateSeasonCopy = () => {
        seasonName.textContent = guide.name;
        seasonDescription.textContent = guide.description;
        recommendationTitle.textContent = seasonalMatches.length ? `Gợi ý cho ${guide.name}` : `Khám phá sản phẩm ${guide.name}`;
    };
    activeSeason = season;
    selectedLookSeason = season;
    const host = document.getElementById('season-studio');
    const weather = document.getElementById('season-weather');
    const particleCount = season === 'autumn' ? 48 : season === 'spring' || season === 'winter' ? 34 : 0;
    weather.innerHTML = Array.from({ length: particleCount }, (_, index) => {
        const left = (index * 47 + 13) % 100;
        const isRain = season === 'autumn';
        const duration = isRain ? 1.5 + (index % 8) * 0.16 : 8 + (index % 7) * 1.4;
        const delay = isRain ? -((index * 0.19) % duration) : -((index * 1.7) % duration);
        const size = season === 'spring' ? 12 + index % 9 : 6 + index % 8;
        return `<span class="season-particle season-particle-${season}" style="--particle-left:${left}%;--particle-duration:${duration}s;--particle-delay:${delay}s;--particle-size:${size}px;--particle-sway:${(index % 2 ? 1 : -1) * (18 + index % 30)}px"></span>`;
    }).join('');
    const catalog = products.filter(product => product.stock > 0).map(product => {
        const details = normalizeCatalogText(`${product.name} ${product.description}`);
        const color = normalizeCatalogText(product.color);
        const score = guide.keywords.reduce((total, keyword) => total + (details.includes(normalizeCatalogText(keyword)) ? 3 : 0), 0)
            + guide.colors.reduce((total, keyword) => total + (color.includes(normalizeCatalogText(keyword)) ? 1 : 0), 0);
        return { product, score };
    });
    const seasonalMatches = catalog.filter(item => item.score > 0).sort((a, b) => b.score - a.score || a.product.name.localeCompare(b.product.name));
    const recommendations = (seasonalMatches.length ? seasonalMatches : catalog).slice(0, 4);
    host.dataset.season = season;
    if (shouldAnimateSeasonCopy) {
        seasonCopy.forEach(element => {
            element.classList.remove('season-copy-entering');
            element.classList.add('season-copy-leaving');
        });
        window.setTimeout(() => {
            if (transitionId !== seasonTextTransitionId) return;
            updateSeasonCopy();
            seasonCopy.forEach(element => {
                element.classList.remove('season-copy-leaving');
                element.classList.add('season-copy-entering');
            });
            window.setTimeout(() => {
                if (transitionId === seasonTextTransitionId) seasonCopy.forEach(element => element.classList.remove('season-copy-entering'));
            }, 650);
        }, 240);
    } else {
        seasonCopy.forEach(element => element.classList.remove('season-copy-leaving', 'season-copy-entering'));
        updateSeasonCopy();
    }
    document.getElementById('season-recommendation-count').textContent = recommendations.length ? `${recommendations.length} sản phẩm` : '';
    document.getElementById('season-products-empty').hidden = recommendations.length > 0;
    const seasonProducts = document.getElementById('season-products');
    const renderSeasonProducts = animateIn => {
        seasonProducts.innerHTML = recommendations.map(({ product }, index) => `
            <article class="season-product-card${animateIn ? ' season-copy-entering' : ''}" style="--season-card-delay:${animateIn ? index * 70 : 0}ms">
                <button type="button" class="season-product-open" data-season-product="${escapeHtml(product.id)}" aria-label="Xem ${escapeHtml(product.name)}">
                    <img src="${imageUrl(product.id, product.image)}" alt="${escapeHtml(product.name)}" loading="lazy" />
                    <span class="season-product-copy"><strong>${escapeHtml(product.name)}</strong><small>${money.format(product.price)}</small></span>
                    <span class="season-product-arrow" aria-hidden="true">↗</span>
                </button>
            </article>`).join('');
        if (animateIn) {
            window.setTimeout(() => {
                if (transitionId === seasonTextTransitionId) seasonProducts.querySelectorAll('.season-copy-entering').forEach(card => card.classList.remove('season-copy-entering'));
            }, 1100);
        }
    };
    if (shouldAnimateSeasonCopy) {
        seasonProducts.querySelectorAll('.season-product-card').forEach(card => {
            card.classList.remove('season-copy-entering');
            card.classList.add('season-copy-leaving');
        });
        window.setTimeout(() => {
            if (transitionId === seasonTextTransitionId) renderSeasonProducts(true);
        }, 240);
    } else {
        renderSeasonProducts(false);
    }
    document.querySelectorAll('[data-season-choice]').forEach(button => {
        const selected = button.dataset.seasonChoice === season;
        button.classList.toggle('is-active', selected);
        button.setAttribute('aria-pressed', String(selected));
    });
    replaySeasonTextReveal();
    renderColorLookOptions();
};
const initializeSeasonStudio = () => {
    const season = getCurrentSeason();
    document.querySelector('.season-picker').addEventListener('click', event => {
        const button = event.target.closest('[data-season-choice]');
        if (button) renderSeason(button.dataset.seasonChoice);
    });
    document.getElementById('season-products').addEventListener('click', event => {
        const button = event.target.closest('[data-season-product]');
        if (button) openProduct(button.dataset.seasonProduct);
    });
    renderSeason(season);
};
let selectedTopColor = 'beige';
let selectedBottomColor = 'brown';
let selectedColorLook = 'daily';
let selectedStylistGender = '';
let selectedStylistBudget = '';
let selectedStylistSize = '';
let savedColorOutfits = readStore('studio.saved-color-outfits', []);
if (!Array.isArray(savedColorOutfits)) savedColorOutfits = [];
savedColorOutfits = savedColorOutfits.filter(outfit => outfit && colorPalettes[outfit.top] && colorPalettes[outfit.bottom]);
const colorLookSeasons = {
    all: 'Tất cả mùa', spring: 'Xuân', summer: 'Hè', autumn: 'Thu', winter: 'Đông'
};
const colorLookSeasonNames = { spring: 'mùa xuân', summer: 'mùa hè', autumn: 'mùa thu', winter: 'mùa đông' };
let selectedLookSeason = 'all';
const colorLooks = {
    daily: { label: 'Hằng ngày', top: 'beige', bottom: 'brown', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'cơ bản thường nhật dạo phố' },
    work: { label: 'Đi làm', top: 'white', bottom: 'navy', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'công sở văn phòng chuyên nghiệp' },
    date: { label: 'Hẹn hò', top: 'pink', bottom: 'beige', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'lãng mạn buổi tối gặp gỡ' },
    event: { label: 'Sự kiện', top: 'black', bottom: 'wine', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'trang trọng quan trọng' },
    picnic: { label: 'Dã ngoại', top: 'white', bottom: 'olive', seasons: ['spring', 'summer'], keywords: 'ngoài trời picnic thoải mái' },
    wedding: { label: 'Dự tiệc cưới', top: 'pink', bottom: 'wine', seasons: ['spring', 'summer', 'autumn'], keywords: 'tiệc cưới lễ trang trọng' },
    travel: { label: 'Du lịch', top: 'sky', bottom: 'beige', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'đi chơi khám phá tiện lợi' },
    beach: { label: 'Đi biển', top: 'white', bottom: 'sky', seasons: ['summer'], keywords: 'nghỉ mát biển resort nắng' },
    festival: { label: 'Lễ hội', top: 'orange', bottom: 'navy', seasons: ['spring', 'summer'], keywords: 'âm nhạc ngoài trời năng động' },
    interview: { label: 'Phỏng vấn', top: 'white', bottom: 'black', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'gặp gỡ công việc chỉn chu' },
    weekend: { label: 'Cuối tuần', top: 'mint', bottom: 'beige', seasons: ['spring', 'summer', 'autumn'], keywords: 'thư giãn dạo phố bạn bè' },
    autumnWalk: { label: 'Dạo phố mùa thu', top: 'rust', bottom: 'beige', seasons: ['autumn'], keywords: 'đi chơi lá vàng ngoài trời' },
    layering: { label: 'Phối lớp mùa đông', top: 'cream', bottom: 'forest', seasons: ['winter'], keywords: 'áo khoác ấm layering lạnh' },
    holiday: { label: 'Lễ hội cuối năm', top: 'wine', bottom: 'black', seasons: ['autumn', 'winter'], keywords: 'giáng sinh năm mới tiệc' },
    dinner: { label: 'Ăn tối', top: 'wine', bottom: 'black', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'nhà hàng buổi tối sang trọng' },
    gym: { label: 'Vận động nhẹ', top: 'blue', bottom: 'black', seasons: ['spring', 'summer', 'autumn', 'winter'], keywords: 'thể thao tập luyện năng động' }
};
const productKind = product => {
    const name = normalizeCatalogText(product.name);
    if (name.includes('dam ') || name.startsWith('dam ') || name.includes('jumpsuit')) return 'onepiece';
    if (name.includes('quan ') || name.includes('chan vay') || name.startsWith('vay ')) return 'bottom';
    if (name.includes('ao ') || name.startsWith('ao ') || name.startsWith('so mi ') || name.includes('blazer') || name.includes('cardigan') || name.includes('sweater') || name.includes('vest ')) return 'top';
    return product.category === 'accessories' ? 'accessory' : 'other';
};
const recommendedAccessoryColors = bottomColor => {
    const neutralBottoms = ['black', 'navy', 'gray'];
    const warmBottoms = ['beige', 'brown', 'rust', 'mustard', 'olive', 'forest'];
    const shoe = neutralBottoms.includes(bottomColor) ? 'black' : warmBottoms.includes(bottomColor) ? 'brown' : 'cream';
    const belt = bottomColor === 'black' ? 'black' : 'brown';
    return { shoe, belt };
};
const renderProductLink = product => `
    <button class="matching-product" type="button" data-product="${escapeHtml(product.id)}">
        <img src="${imageUrl(product.id, product.image)}" alt="" loading="lazy" />
        <span><strong>${escapeHtml(product.name)}</strong><small>${escapeHtml(product.color)} · ${money.format(product.price)}</small></span><span class="matching-product-arrow" aria-hidden="true">↗</span>
    </button>`;
const apparelSizeOrder = ['XS', 'S', 'M', 'L', 'XL', 'XXL', 'XXXL'];
const estimateApparelSize = (heightCm, weightKg, category) => {
    const bmi = weightKg / ((heightCm / 100) ** 2);
    const sizeIndex = category === 'women'
        ? bmi < 17.5 ? 0 : bmi < 19.5 ? 1 : bmi < 22.5 ? 2 : bmi < 26 ? 3 : 4
        : bmi < 18.5 ? 0 : bmi < 21.5 ? 1 : bmi < 24.5 ? 2 : bmi < 27.5 ? 3 : 4;
    const preferredSizes = category === 'women' ? apparelSizeOrder : apparelSizeOrder.slice(1);
    const candidateSizes = [...new Set(products
        .filter(product => product.category === category && product.stock > 0 && productKind(product) !== 'accessory')
        .flatMap(product => product.sizes)
        .map(size => size.toUpperCase()))];
    if (!candidateSizes.length) return null;
    const rankedAvailableSizes = candidateSizes
        .filter(size => preferredSizes.includes(size))
        .sort((a, b) => preferredSizes.indexOf(a) - preferredSizes.indexOf(b));
    if (!rankedAvailableSizes.length) return null;
    return rankedAvailableSizes.reduce((closest, size) =>
        Math.abs(preferredSizes.indexOf(size) - sizeIndex) < Math.abs(preferredSizes.indexOf(closest) - sizeIndex) ? size : closest);
};
const renderSizeRecommendation = () => {
    const host = document.getElementById('stylist-size-recommendation');
    const height = Number(document.getElementById('stylist-height-filter').value);
    const weight = Number(document.getElementById('stylist-weight-filter').value);
    if (!height || !weight || height < 100 || height > 230 || weight < 20 || weight > 250) {
        host.hidden = true;
        host.replaceChildren();
        return;
    }

    const categories = selectedStylistGender ? [selectedStylistGender] : ['women', 'men'];
    const categoryLabels = { women: 'Đồ nữ', men: 'Đồ nam' };
    const suggestions = categories.map(category => ({ category, size: estimateApparelSize(height, weight, category) }))
        .filter(suggestion => suggestion.size);
    if (!suggestions.length) {
        host.textContent = 'Chưa có đủ dữ liệu size sản phẩm để đưa ra gợi ý.';
    } else {
        host.innerHTML = `<strong>Size tham khảo theo số đo:</strong> ${suggestions.map(({ category, size }) => `<button type="button" class="stylist-size-suggestion" data-recommended-size="${escapeHtml(size)}" aria-label="Lọc sản phẩm ${categoryLabels[category]} size ${escapeHtml(size)}">${categoryLabels[category]}: <b>${escapeHtml(size)}</b></button>`).join(' ')} <span>Chọn size để lọc sản phẩm.</span><button type="button" class="stylist-size-clear">Bỏ chọn</button>`;
    }
    host.hidden = false;
};
const matchesStylistFilters = product => {
    const genderMatches = !selectedStylistGender || product.category === 'accessories' || product.category === selectedStylistGender;
    const budgetMatches = !selectedStylistBudget || product.price <= Number(selectedStylistBudget);
    const sizeMatches = !selectedStylistSize || productKind(product) === 'accessory' || !product.sizes.length || product.sizes.includes(selectedStylistSize);
    return genderMatches && budgetMatches && sizeMatches;
};
const renderProductList = (hostId, countId, emptyId, matches, emptyText) => {
    const filteredMatches = matches.filter(matchesStylistFilters);
    document.getElementById(hostId).innerHTML = filteredMatches.slice(0, 4).map(renderProductLink).join('');
    document.getElementById(countId).textContent = filteredMatches.length ? `${filteredMatches.length} món` : '';
    const empty = document.getElementById(emptyId);
    empty.textContent = matches.length && !filteredMatches.length
        ? 'Không có sản phẩm phù hợp với giới tính, ngân sách hoặc kích cỡ đã chọn.'
        : emptyText;
    empty.hidden = filteredMatches.length > 0;
};
const renderColorStylist = () => {
    const topColor = colorPalettes[selectedTopColor];
    const bottomColor = colorPalettes[selectedBottomColor];
    for (const [hostId, selectedColor, attribute] of [['top-color-options', selectedTopColor, 'data-top-color'], ['bottom-color-options', selectedBottomColor, 'data-bottom-color']]) {
        document.getElementById(hostId).innerHTML = Object.entries(colorPalettes).map(([key, color]) => `<button type="button" class="base-color-option ${key === selectedColor ? 'is-selected' : ''}" ${attribute}="${key}" aria-label="Chọn màu ${color.label}" aria-pressed="${key === selectedColor}"><i style="--swatch-color:${color.hex}"></i><span>${color.label}</span></button>`).join('');
    }
    const model = document.querySelector('.color-model');
    model.style.setProperty('--model-top', topColor.hex);
    model.style.setProperty('--model-bottom', bottomColor.hex);
    document.getElementById('color-model-caption').textContent = `${topColor.label} + ${bottomColor.label}`;
    document.getElementById('model-top-swatch').style.backgroundColor = topColor.hex;
    document.getElementById('model-top-name').textContent = topColor.label;
    document.getElementById('model-bottom-swatch').style.backgroundColor = bottomColor.hex;
    document.getElementById('model-bottom-name').textContent = bottomColor.label;

    const normalizedPair = topColor.matches.includes(selectedBottomColor);
    document.getElementById('color-combination-tip').textContent = normalizedPair
        ? topColor.tip
        : `${topColor.label} và ${bottomColor.label} tạo tương phản rõ nét. Giữ phụ kiện trung tính để tổng thể hài hòa hơn.`;
    const accessoryColors = recommendedAccessoryColors(selectedBottomColor);
    const shoe = colorPalettes[accessoryColors.shoe];
    const belt = colorPalettes[accessoryColors.belt];
    document.getElementById('color-accessory-guidance').innerHTML = `
        <span class="accessory-guidance-item"><i style="--swatch-color:${shoe.hex}"></i><span><strong>Giày: ${shoe.label}</strong><small>Chọn tông này để cân bằng màu quần / váy.</small></span></span>
        <span class="accessory-guidance-item"><i style="--swatch-color:${belt.hex}"></i><span><strong>Thắt lưng: ${belt.label}</strong><small>Ưu tiên cùng tông với giày để outfit gọn mắt.</small></span></span>`;

    document.getElementById('color-look-label').textContent = selectedColorLook ? `Gợi ý cho dịp: ${colorLooks[selectedColorLook].label}` : 'Bản phối tùy chỉnh';
    renderColorLookOptions();
    renderSavedColorOutfits();

    const available = products.filter(product => product.stock > 0);
    const tops = available.filter(product => productKind(product) === 'top' && colorKeyFor(product.color) === selectedTopColor);
    const bottoms = available.filter(product => productKind(product) === 'bottom' && colorKeyFor(product.color) === selectedBottomColor);
    const accessoryKeys = new Set([accessoryColors.shoe, accessoryColors.belt, selectedTopColor]);
    const accessories = available.filter(product => productKind(product) === 'accessory' && accessoryKeys.has(colorKeyFor(product.color)));
    renderProductList('top-matching-products', 'top-products-count', 'top-products-empty', tops, 'Chưa có áo đúng màu này trong shop.');
    renderProductList('bottom-matching-products', 'bottom-products-count', 'bottom-products-empty', bottoms, 'Chưa có quần hoặc váy đúng màu này trong shop.');
    renderProductList('accessory-matching-products', 'accessory-products-count', 'accessory-products-empty', accessories, 'Chưa có phụ kiện hợp màu trong shop; bạn vẫn có thể tham khảo gợi ý màu giày và thắt lưng.');
};
const renderColorLookOptions = () => {
    const seasonHost = document.getElementById('color-look-seasons');
    seasonHost.innerHTML = Object.entries(colorLookSeasons).map(([key, label]) => `<button type="button" data-color-season="${key}" class="${key === selectedLookSeason ? 'is-active' : ''}" aria-pressed="${key === selectedLookSeason}">${label}</button>`).join('');
    const query = normalizeCatalogText(document.getElementById('color-look-search').value.trim());
    const queryTerms = query.split(/\s+/).filter(term => term.length >= 3);
    const matches = Object.entries(colorLooks).filter(([, look]) => {
        const matchesSeason = selectedLookSeason === 'all' || look.seasons.includes(selectedLookSeason);
        const searchableText = normalizeCatalogText(`${look.label} ${look.keywords} ${look.seasons.map(season => `${colorLookSeasons[season]} ${colorLookSeasonNames[season]}`).join(' ')}`);
        const matchesSearch = !queryTerms.length || queryTerms.some(term => searchableText.includes(term));
        return matchesSeason && matchesSearch;
    });
    document.getElementById('color-look-presets').innerHTML = matches.map(([key, look]) => `<button type="button" data-color-look="${key}" class="${key === selectedColorLook ? 'is-active' : ''}" aria-pressed="${key === selectedColorLook}" title="${escapeHtml(look.keywords)}">${escapeHtml(look.label)}</button>`).join('');
    document.getElementById('color-look-empty').hidden = matches.length > 0;
    document.getElementById('clear-color-look-search').hidden = !query;
};
const renderSavedColorOutfits = () => {
    const host = document.getElementById('saved-color-outfits');
    host.innerHTML = savedColorOutfits.map((outfit, index) => {
        const top = colorPalettes[outfit?.top];
        const bottom = colorPalettes[outfit?.bottom];
        if (!top || !bottom) return '';
        return `<span class="saved-color-outfit"><button type="button" data-saved-color-outfit="${index}" aria-label="Dùng bản phối áo ${top.label}, quần ${bottom.label}"><i style="--swatch-color:${top.hex}"></i><i style="--swatch-color:${bottom.hex}"></i>${escapeHtml(outfit.label || `${top.label} + ${bottom.label}`)}</button><button type="button" data-remove-color-outfit="${index}" aria-label="Xóa bản phối đã lưu">×</button></span>`;
    }).join('');
    host.hidden = savedColorOutfits.length === 0;
};
const saveCurrentColorOutfit = () => {
    const outfit = { top: selectedTopColor, bottom: selectedBottomColor, label: selectedColorLook ? colorLooks[selectedColorLook].label : 'Tùy chỉnh' };
    savedColorOutfits = savedColorOutfits.filter(saved => saved.top !== outfit.top || saved.bottom !== outfit.bottom);
    savedColorOutfits.unshift(outfit);
    savedColorOutfits = savedColorOutfits.slice(0, 6);
    if (saveStore('studio.saved-color-outfits', savedColorOutfits)) {
        renderSavedColorOutfits();
        showToast('Đã lưu bản phối của bạn.');
    }
};
const copyCurrentColorOutfit = async () => {
    const top = colorPalettes[selectedTopColor];
    const bottom = colorPalettes[selectedBottomColor];
    const accessories = recommendedAccessoryColors(selectedBottomColor);
    const summary = `W Studio: Áo ${top.label}, quần/váy ${bottom.label}; giày ${colorPalettes[accessories.shoe].label}, thắt lưng ${colorPalettes[accessories.belt].label}.`;
    try {
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(summary);
        } else {
            const temporaryInput = document.createElement('textarea');
            temporaryInput.value = summary;
            temporaryInput.setAttribute('readonly', '');
            temporaryInput.style.position = 'fixed';
            temporaryInput.style.opacity = '0';
            document.body.append(temporaryInput);
            temporaryInput.select();
            const copied = document.execCommand('copy');
            temporaryInput.remove();
            if (!copied) throw new Error('Clipboard copy was rejected.');
        }
        showToast('Đã sao chép gợi ý phối đồ.');
    } catch {
        showToast('Không thể sao chép gợi ý phối đồ.');
    }
};
const renderProductOutfitSuggestions = product => {
    const colorKey = colorKeyFor(product.color) || 'beige';
    const palette = colorPalettes[colorKey];
    const kind = productKind(product);
    const complementaryKind = kind === 'top' ? 'bottom' : kind === 'bottom' ? 'top' : null;
    const garments = products.filter(candidate => candidate.id !== product.id && candidate.stock > 0
        && candidate.category !== 'accessories'
        && (!complementaryKind || productKind(candidate) === complementaryKind)
        && palette.matches.includes(colorKeyFor(candidate.color)));
    const accessoryColors = recommendedAccessoryColors(colorKeyFor(product.color) || 'beige');
    const accessoryKeys = new Set([accessoryColors.shoe, accessoryColors.belt, ...palette.matches]);
    const accessories = products.filter(candidate => candidate.id !== product.id && productKind(candidate) === 'accessory'
        && candidate.stock > 0 && accessoryKeys.has(colorKeyFor(candidate.color))).slice(0, 3);
    const garmentHeading = complementaryKind === 'bottom' ? 'Quần / váy phối cùng' : complementaryKind === 'top' ? 'Áo phối cùng' : 'Trang phục hợp màu';
    const garmentCards = garments.slice(0, 2).map(renderProductLink).join('');
    const accessoryCards = accessories.slice(0, 2).map(renderProductLink).join('');
    return `<section class="detail-outfit-suggestions" aria-label="Gợi ý phối đồ với sản phẩm này">
        <div class="detail-outfit-heading"><button class="detail-back-button" type="button" data-product-back hidden>← Quay lại sản phẩm trước</button><span class="section-kicker">W STUDIO · OUTFIT IDEAS</span><h3>Gợi ý phối cùng sản phẩm này</h3><p>${escapeHtml(palette.tip)}</p></div>
        <div class="detail-outfit-colors"><span><i style="--swatch-color:${colorPalettes[accessoryColors.shoe].hex}"></i><strong>Giày ${colorPalettes[accessoryColors.shoe].label}</strong></span><span><i style="--swatch-color:${colorPalettes[accessoryColors.belt].hex}"></i><strong>Thắt lưng ${colorPalettes[accessoryColors.belt].label}</strong></span></div>
        <div class="detail-outfit-group"><h4>${garmentHeading}</h4><div class="detail-outfit-products">${garmentCards || '<p class="matching-products-empty">Chưa có món phối cùng đúng tông màu này trong shop.</p>'}</div></div>
        <div class="detail-outfit-group"><h4>Phụ kiện gợi ý</h4><div class="detail-outfit-products">${accessoryCards || '<p class="matching-products-empty">Tham khảo màu giày và thắt lưng gợi ý để hoàn thiện outfit.</p>'}</div></div>
    </section>`;
};
const initializeColorStylist = () => {
    const genderFilter = document.getElementById('stylist-gender-filter');
    genderFilter.addEventListener('change', event => {
        selectedStylistGender = event.target.value;
        renderSizeRecommendation();
        renderColorStylist();
    });
    for (const inputId of ['stylist-height-filter', 'stylist-weight-filter']) {
        document.getElementById(inputId).addEventListener('input', renderSizeRecommendation);
    }
    const sizeFilter = document.getElementById('stylist-size-filter');
    document.getElementById('stylist-size-recommendation').addEventListener('click', event => {
        if (event.target.closest('.stylist-size-clear')) {
            selectedStylistSize = '';
            sizeFilter.value = '';
            renderColorStylist();
            return;
        }
        const button = event.target.closest('[data-recommended-size]');
        if (!button) return;
        selectedStylistSize = button.dataset.recommendedSize;
        sizeFilter.value = selectedStylistSize;
        renderColorStylist();
    });
    [...new Set(products.flatMap(product => product.sizes).filter(Boolean))].sort((a, b) => a.localeCompare(b, 'vi')).forEach(size => {
        const option = document.createElement('option');
        option.value = size;
        option.textContent = size;
        sizeFilter.append(option);
    });
    document.getElementById('stylist-budget-filter').addEventListener('change', event => {
        selectedStylistBudget = event.target.value;
        renderColorStylist();
    });
    sizeFilter.addEventListener('change', event => {
        selectedStylistSize = event.target.value;
        renderColorStylist();
    });
    document.getElementById('top-color-options').addEventListener('click', event => {
        const button = event.target.closest('[data-top-color]');
        if (!button) return;
        selectedTopColor = button.dataset.topColor;
        selectedColorLook = null;
        renderColorStylist();
    });
    document.getElementById('bottom-color-options').addEventListener('click', event => {
        const button = event.target.closest('[data-bottom-color]');
        if (!button) return;
        selectedBottomColor = button.dataset.bottomColor;
        selectedColorLook = null;
        renderColorStylist();
    });
    document.getElementById('color-look-search').addEventListener('input', renderColorLookOptions);
    document.getElementById('color-stylist').addEventListener('click', event => {
        const seasonButton = event.target.closest('[data-color-season]');
        if (seasonButton) {
            const season = seasonButton.dataset.colorSeason;
            if (season === 'all') {
                selectedLookSeason = 'all';
                renderColorLookOptions();
            } else {
                renderSeason(season);
            }
            return;
        }
        if (event.target.closest('#clear-color-look-search')) {
            document.getElementById('color-look-search').value = '';
            renderColorLookOptions();
            document.getElementById('color-look-search').focus();
            return;
        }
        const lookButton = event.target.closest('[data-color-look]');
        if (lookButton) {
            const look = colorLooks[lookButton.dataset.colorLook];
            selectedColorLook = lookButton.dataset.colorLook;
            selectedTopColor = look.top;
            selectedBottomColor = look.bottom;
            if (look.seasons.includes(activeSeason)) selectedLookSeason = activeSeason;
            else renderSeason(look.seasons[0]);
            renderColorStylist();
            return;
        }
        const actionButton = event.target.closest('[data-color-action]');
        if (actionButton) {
            if (actionButton.dataset.colorAction === 'save') saveCurrentColorOutfit();
            if (actionButton.dataset.colorAction === 'copy') copyCurrentColorOutfit();
            if (actionButton.dataset.colorAction === 'reset') {
                selectedTopColor = 'beige';
                selectedBottomColor = 'brown';
                selectedColorLook = 'daily';
                selectedLookSeason = 'all';
                selectedStylistGender = '';
                selectedStylistBudget = '';
                selectedStylistSize = '';
                document.getElementById('stylist-gender-filter').value = '';
                document.getElementById('stylist-budget-filter').value = '';
                document.getElementById('stylist-height-filter').value = '';
                document.getElementById('stylist-weight-filter').value = '';
                document.getElementById('stylist-size-filter').value = '';
                renderSizeRecommendation();
                document.getElementById('color-look-search').value = '';
                renderColorStylist();
            }
            if (actionButton.dataset.colorAction === 'random') {
                const topKeys = Object.keys(colorPalettes);
                selectedTopColor = topKeys[Math.floor(Math.random() * topKeys.length)];
                const matchingColors = colorPalettes[selectedTopColor].matches;
                selectedBottomColor = matchingColors[Math.floor(Math.random() * matchingColors.length)];
                selectedColorLook = null;
                renderColorStylist();
            }
            return;
        }
        const savedButton = event.target.closest('[data-saved-color-outfit]');
        if (savedButton) {
            const outfit = savedColorOutfits[Number(savedButton.dataset.savedColorOutfit)];
            if (outfit && colorPalettes[outfit.top] && colorPalettes[outfit.bottom]) {
                selectedTopColor = outfit.top;
                selectedBottomColor = outfit.bottom;
                selectedColorLook = null;
                renderColorStylist();
            }
            return;
        }
        const removeButton = event.target.closest('[data-remove-color-outfit]');
        if (removeButton) {
            savedColorOutfits.splice(Number(removeButton.dataset.removeColorOutfit), 1);
            if (saveStore('studio.saved-color-outfits', savedColorOutfits)) renderSavedColorOutfits();
            return;
        }
        const button = event.target.closest('[data-product]');
        if (button) openProduct(button.dataset.product);
    });
    renderColorStylist();
};
const openProduct = (id, navigation = 'new') => {
    const nextProduct = products.find(product => product.id === id);
    if (!nextProduct) return;
    if (navigation === 'suggested' && selectedProduct) productHistory.push({ id: selectedProduct.id, size: selectedSize });
    else if (navigation === 'new') productHistory.length = 0;
    selectedProduct = nextProduct;
    selectedSize = selectedProduct.sizes[0];
    const productDialog = document.getElementById('product-dialog');
    document.getElementById('product-detail').innerHTML = `
        <img class="detail-image" src="${imageUrl(selectedProduct.id, selectedProduct.image)}" alt="${escapeHtml(selectedProduct.name)}" />
        <div class="detail-info-panel"><div class="detail-copy"><span class="section-kicker">${escapeHtml(selectedProduct.categoryLabel)}</span><h2 id="product-name">${escapeHtml(selectedProduct.name)}</h2><p class="detail-price">${money.format(selectedProduct.price)}</p><div class="product-rating detail-rating">${selectedProduct.review.reviewCount ? `★ ${selectedProduct.review.averageRating.toFixed(1)} <span>· ${selectedProduct.review.reviewCount} đánh giá</span>` : '<span>Chưa có đánh giá</span>'}</div><p class="detail-description">${escapeHtml(selectedProduct.description)}</p><p class="detail-color">Màu sắc: <strong>${escapeHtml(selectedProduct.color)}</strong></p><p class="detail-color">Còn lại: <strong>${selectedProduct.stock}</strong></p><span class="size-label">CHỌN KÍCH CỠ</span><div class="size-options">${selectedProduct.sizes.map((size, index) => `<button type="button" data-size="${escapeHtml(size)}" class="size-option ${index === 0 ? 'is-selected' : ''}" aria-pressed="${index === 0}">${escapeHtml(size)}</button>`).join('')}</div><div class="detail-purchase-actions"><button id="detail-add" class="solid-button" type="button" ${selectedProduct.stock < 1 ? 'disabled' : ''}>${selectedProduct.stock < 1 ? 'TẠM HẾT HÀNG' : 'THÊM VÀO TÚI'} <span aria-hidden="true">＋</span></button><button id="detail-buy-now" class="solid-button detail-buy-now" type="button" ${selectedProduct.stock < 1 ? 'disabled' : ''}>MUA NGAY <span aria-hidden="true">→</span></button></div><p id="detail-cart-status" class="detail-cart-status" role="status" aria-live="polite" hidden></p></div>${renderProductOutfitSuggestions(selectedProduct)}</div>`;
    const backButton = document.querySelector('[data-product-back]');
    backButton.hidden = productHistory.length === 0;
    productDialog.scrollTop = 0;
    productDialog.showModal();
};

initializeCatalogFilters();
renderProducts();
renderCart();
initializeColorStylist();
initializeSeasonStudio();
const pendingPurchase = readStore('studio.pending-purchase', null);
if (pendingPurchase && typeof pendingPurchase.id === 'string' && typeof pendingPurchase.size === 'string') {
    const pendingProduct = products.find(product => product.id === pendingPurchase.id);
    const pendingItemInCart = cart.some(item => item.id === pendingPurchase.id && item.size === pendingPurchase.size);
    if (pendingProduct && pendingProduct.sizes.includes(pendingPurchase.size) && pendingItemInCart) {
        openProduct(pendingProduct.id);
        selectedSize = pendingPurchase.size;
        document.querySelectorAll('.size-option').forEach(button => {
            const selected = button.dataset.size === selectedSize;
            button.classList.toggle('is-selected', selected);
            button.setAttribute('aria-pressed', String(selected));
        });
        resumedPendingPurchase = { id: pendingProduct.id, size: pendingPurchase.size };
    }
    saveStore('studio.pending-purchase', null);
}
const requestedProductId = new URLSearchParams(window.location.search).get('product');
if (requestedProductId && products.some(product => product.id === requestedProductId)) {
    requestAnimationFrame(() => openProduct(requestedProductId));
}
document.getElementById('cart-empty').querySelector('.text-link').addEventListener('click', () => document.getElementById('collection').scrollIntoView({ behavior: 'smooth' }));
document.getElementById('products-previous').addEventListener('click', () => {
    currentProductPage--;
    renderProducts();
    document.getElementById('collection').scrollIntoView({ behavior: 'smooth' });
});
document.getElementById('products-next').addEventListener('click', () => {
    currentProductPage++;
    renderProducts();
    document.getElementById('collection').scrollIntoView({ behavior: 'smooth' });
});
document.getElementById('favorites-filter').addEventListener('click', event => {
    favoritesOnly = !favoritesOnly;
    event.currentTarget.classList.toggle('is-active', favoritesOnly);
    event.currentTarget.setAttribute('aria-pressed', String(favoritesOnly));
    resetProductPage();
    renderProducts();
});
document.querySelectorAll('.category-tab').forEach(button => button.addEventListener('click', () => {
    selectedCategory = button.dataset.category;
    document.querySelectorAll('.category-tab').forEach(tab => {
        const active = tab === button;
        tab.classList.toggle('is-active', active);
        tab.setAttribute('aria-pressed', String(active));
    });
    resetProductPage();
    renderProducts();
}));
document.querySelectorAll('[data-nav-category]').forEach(link => link.addEventListener('click', () => {
    selectedCategory = link.dataset.navCategory;
    const activeTab = document.querySelector(`.category-tab[data-category="${selectedCategory}"]`);
    if (activeTab) activeTab.click();
}));
const searchInput = document.getElementById('search-input');
let catalogSearchScrollTimer;
const scrollToCatalogResults = () => {
    const target = document.querySelector('#product-grid .product-card') || document.getElementById('empty-products');
    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
};
searchInput.addEventListener('input', () => {
    resetProductPage();
    renderProducts();
    clearTimeout(catalogSearchScrollTimer);
    if (!searchInput.value.trim()) return;
    catalogSearchScrollTimer = setTimeout(scrollToCatalogResults, 500);
});
searchInput.addEventListener('keydown', event => {
    if (event.key !== 'Enter' || !searchInput.value.trim()) return;
    clearTimeout(catalogSearchScrollTimer);
    scrollToCatalogResults();
});
['color-filter', 'size-filter', 'price-filter'].forEach(id => document.getElementById(id).addEventListener('change', () => { resetProductPage(); renderProducts(); }));
document.getElementById('sort-select').addEventListener('change', () => { resetProductPage(); renderProducts(); });
document.getElementById('product-grid').addEventListener('click', event => {
    const favoriteButton = event.target.closest('[data-favorite]');
    if (favoriteButton) {
        const id = favoriteButton.dataset.favorite;
        if (favorites.has(id)) favorites.delete(id); else favorites.add(id);
        saveStore('studio.favorites', [...favorites]);
        renderProducts();
        return;
    }
    const productButton = event.target.closest('[data-product]');
    if (productButton) openProduct(productButton.dataset.product);
});
document.getElementById('product-detail').addEventListener('click', event => {
    if (event.target.closest('[data-product-back]')) {
        const previousProduct = productHistory.pop();
        if (previousProduct) {
            openProduct(previousProduct.id, 'back');
            if (previousProduct.size && selectedProduct.sizes.includes(previousProduct.size)) {
                selectedSize = previousProduct.size;
                document.querySelector(`.size-option[data-size="${CSS.escape(selectedSize)}"]`)?.click();
            }
        }
        return;
    }
    const suggestedProduct = event.target.closest('.detail-outfit-suggestions [data-product]');
    if (suggestedProduct) {
        openProduct(suggestedProduct.dataset.product, 'suggested');
        return;
    }
    const sizeButton = event.target.closest('[data-size]');
    if (sizeButton) {
        selectedSize = sizeButton.dataset.size;
        document.querySelectorAll('.size-option').forEach(button => {
            const selected = button === sizeButton;
            button.classList.toggle('is-selected', selected);
            button.setAttribute('aria-pressed', String(selected));
        });
    }
    const addButton = event.target.closest('#detail-add, #detail-buy-now');
    if (addButton && selectedProduct) {
        if (addButton.id === 'detail-buy-now'
            && resumedPendingPurchase?.id === selectedProduct.id
            && resumedPendingPurchase.size === selectedSize) {
            resumedPendingPurchase = null;
            beginCheckout();
            return;
        }
        const status = document.getElementById('detail-cart-status');
        const existing = cart.find(item => item.id === selectedProduct.id && item.size === selectedSize);
        if ((existing?.quantity ?? 0) >= Math.min(10, selectedProduct.stock)) {
            status.textContent = 'Đã đạt số lượng tối đa hiện có của sản phẩm này.';
            status.hidden = false;
            showToast(status.textContent);
            return;
        }
        if (!addProductToCart(selectedProduct, selectedSize, false)) {
            status.textContent = 'Sản phẩm hoặc size này hiện không khả dụng.';
            status.hidden = false;
            return;
        }
        status.textContent = `${selectedProduct.name} · size ${selectedSize} đã được thêm vào túi.`;
        status.hidden = false;
        showToast('Đã thêm vào giỏ hàng.');
        if (addButton.id === 'detail-buy-now') {
            if (!isAuthenticated) saveStore('studio.pending-purchase', { id: selectedProduct.id, size: selectedSize });
            beginCheckout();
        }
    }
});
document.getElementById('open-cart').addEventListener('click', () => document.getElementById('cart-dialog').showModal());
document.getElementById('cart-items').addEventListener('click', event => {
    const removeButton = event.target.closest('[data-remove]');
    const quantityButton = event.target.closest('[data-quantity]');
    if (removeButton) cart.splice(Number(removeButton.dataset.remove), 1);
    if (quantityButton) {
        const item = cart[Number(quantityButton.dataset.quantity)];
        item.quantity += Number(quantityButton.dataset.delta);
        if (item.quantity <= 0) cart.splice(Number(quantityButton.dataset.quantity), 1);
        else if (item.quantity > 10) item.quantity = 10;
    }
    saveCart();
    renderCart();
});
const checkoutAddress = document.getElementById('checkout-address');
const checkoutAddressEdit = document.querySelector('[data-edit-checkout-address]');
const checkoutAddressHint = document.querySelector('.checkout-address-hint');
const resetCheckoutAddress = () => {
    const savedAddress = checkoutAddress.dataset.savedAddress || '';
    checkoutAddress.value = savedAddress;
    checkoutAddress.readOnly = Boolean(savedAddress);
    checkoutAddressEdit.hidden = !savedAddress;
    checkoutAddressEdit.textContent = 'SỬA ĐỊA CHỈ';
    checkoutAddressHint.textContent = savedAddress
        ? 'Địa chỉ từ hồ sơ của bạn. Địa chỉ mới sẽ được lưu làm mặc định.'
        : 'Địa chỉ này sẽ được lưu vào hồ sơ để bạn dùng cho lần đặt hàng sau.';
};
checkoutAddressEdit.addEventListener('click', () => {
    const savedAddress = checkoutAddress.dataset.savedAddress || '';
    if (checkoutAddress.readOnly) {
        checkoutAddress.readOnly = false;
        checkoutAddressEdit.textContent = 'DÙNG ĐỊA CHỈ ĐÃ LƯU';
        checkoutAddressHint.textContent = 'Nhập địa chỉ khác; địa chỉ mới sẽ được lưu làm mặc định.';
        checkoutAddress.focus();
        return;
    }

    checkoutAddress.value = savedAddress;
    checkoutAddress.readOnly = true;
    checkoutAddressEdit.textContent = 'SỬA ĐỊA CHỈ';
    checkoutAddressHint.textContent = 'Địa chỉ từ hồ sơ của bạn. Địa chỉ mới sẽ được lưu làm mặc định.';
});
const beginCheckout = () => {
    if (!cart.length) return;
    if (!isCustomer) {
        if (isAuthenticated) {
            showToast('Đặt hàng chỉ dành cho tài khoản khách hàng. Hãy đăng xuất và đăng nhập bằng tài khoản khách hàng.');
            return;
        }
        redirectToLoginFromStore();
        return;
    }
    const cartDialog = document.getElementById('cart-dialog');
    if (cartDialog.open) cartDialog.close();
    resetCheckoutAddress();
    document.getElementById('checkout-form-wrap').hidden = false;
    document.getElementById('checkout-success').hidden = true;
    document.getElementById('checkout-dialog').showModal();
};
document.getElementById('checkout-button').addEventListener('click', beginCheckout);
document.getElementById('checkout-form').addEventListener('submit', async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const errorMessage = document.getElementById('checkout-error');
    errorMessage.hidden = true;
    if (!form.reportValidity() || !cart.length) return;
    const submitButton = form.querySelector('button[type="submit"]');
    submitButton.disabled = true;
    const token = document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]').value;
    const formValues = new FormData(form);
    try {
        const response = await fetch('/Orders/Create', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
            body: JSON.stringify({
                customerName: formValues.get('CustomerName'),
                phone: formValues.get('Phone'),
                address: formValues.get('Address'),
                note: formValues.get('Note'),
                items: cart.map(item => ({ productSlug: item.id, size: item.size, quantity: item.quantity }))
            })
        });
        const result = await response.json().catch(() => ({}));
        if (response.status === 401) {
            redirectToLoginFromStore();
            return;
        }
        if (!response.ok) {
            const validationErrors = result.errors ? Object.values(result.errors).flat().join(' ') : '';
            throw new Error(result.error || validationErrors || `Không thể tạo đơn hàng (HTTP ${response.status}). Vui lòng kiểm tra thông tin và thử lại.`);
        }
        if (!Number.isInteger(result.orderId) || typeof result.total !== 'number') {
            throw new Error('Máy chủ chưa xác nhận được đơn hàng. Vui lòng kiểm tra lịch sử đơn trước khi thử lại.');
        }
        const submittedAddress = String(formValues.get('Address') || '').trim();
        checkoutAddress.dataset.savedAddress = submittedAddress;
        checkoutAddress.defaultValue = submittedAddress;
        const confirmation = `Đơn hàng #${String(result.orderId).padStart(6, '0')} đã được lưu. Tổng thanh toán COD: ${money.format(result.total)}.`;
        document.getElementById('order-confirmation').textContent = result.customerNotificationSent
            ? `${confirmation} Email xác nhận đã được gửi.`
            : `${confirmation} Bạn có thể theo dõi trạng thái trong mục Tài khoản của tôi.`;
        document.getElementById('checkout-form-wrap').hidden = true;
        document.getElementById('checkout-success').hidden = false;
        cart = [];
        saveCart();
        renderCart();
        form.reset();
        resetCheckoutAddress();
    } catch (error) {
        errorMessage.textContent = error.message;
        errorMessage.hidden = false;
    } finally {
        submitButton.disabled = false;
    }
});
document.querySelectorAll('[data-close]').forEach(button => button.addEventListener('click', () => document.getElementById(button.dataset.close).close()));
document.querySelectorAll('dialog').forEach(dialog => dialog.addEventListener('click', event => {
    if (event.target === dialog) dialog.close();
}));

const chatPanel = document.getElementById('chat-panel');
const openChat = () => {
    if (!isAuthenticated) {
        redirectToLoginFromStore();
        return;
    }
    chatPanel.hidden = false;
    if (isCustomer) loadCurrentSupportChat();
    if (!supportConversationId) renderAiChatHistory();
    document.getElementById('chat-input')?.focus();
};
document.getElementById('chat-launcher').addEventListener('click', openChat);
document.getElementById('open-chat-from-banner').addEventListener('click', openChat);
document.getElementById('close-chat').addEventListener('click', () => { chatPanel.hidden = true; });
const expandChatButton = document.getElementById('expand-chat');
expandChatButton.addEventListener('click', () => {
    const expanded = chatPanel.classList.toggle('is-expanded');
    expandChatButton.setAttribute('aria-pressed', String(expanded));
    expandChatButton.setAttribute('aria-label', expanded ? 'Thu nhỏ khung chat' : 'Mở rộng khung chat');
    expandChatButton.title = expanded ? 'Thu nhỏ khung chat' : 'Mở rộng khung chat';
    expandChatButton.textContent = expanded ? '↙' : '↗';
});
document.getElementById('chat-messages').addEventListener('click', event => {
    const addButton = event.target.closest('[data-add-product]');
    if (addButton) {
        const card = addButton.closest('.chat-product-card');
        const product = products.find(candidate => candidate.id === addButton.dataset.addProduct);
        addProductToCart(product, card.querySelector('.chat-product-size').value);
        return;
    }
    const productButton = event.target.closest('[data-product]');
    if (productButton) openProduct(productButton.dataset.product);
});
document.getElementById('stylist-shortcuts').addEventListener('click', event => {
    const shortcut = event.target.closest('[data-stylist-prompt]');
    if (!shortcut) return;
    document.getElementById('chat-input').value = shortcut.dataset.stylistPrompt;
    document.getElementById('chat-form').requestSubmit();
});
const appendChatMessage = (role, text, pending = false, productCodes = []) => {
    const bubble = document.createElement('div');
    bubble.className = `chat-bubble ${role === 'user' ? 'user-bubble' : 'assistant-bubble'}${pending ? ' is-pending' : ''}`;
    bubble.textContent = text;
    document.getElementById('chat-messages').append(bubble);
    if (!pending && role === 'assistant') renderRecommendationCards(productCodes, bubble);
    bubble.scrollIntoView({ block: 'end', behavior: 'smooth' });
    return bubble;
};
const addProductToCart = (product, size, announce = true) => {
    if (!product || product.stock < 1 || !product.sizes.includes(size)) {
        showToast('Sản phẩm hoặc size này hiện không khả dụng.');
        return false;
    }
    const existing = cart.find(item => item.id === product.id && item.size === size);
    if ((existing?.quantity ?? 0) >= Math.min(10, product.stock)) {
        showToast('Đã đạt số lượng tối đa hiện có của sản phẩm này.');
        return false;
    }
    if (existing) existing.quantity += 1;
    else cart.push({ id: product.id, size, quantity: 1 });
    saveCart();
    renderCart();
    if (announce) showToast(`Đã thêm ${product.name} · size ${size} vào túi.`);
    return true;
};
const renderRecommendationCards = (productCodes, bubble) => {
    const recommendedProducts = productCodes
        .map(code => products.find(product => product.id === code))
        .filter(product => product && product.stock > 0);
    if (!recommendedProducts.length) return;

    const cards = document.createElement('div');
    cards.className = 'chat-recommendations';
    for (const product of recommendedProducts) {
        const card = document.createElement('article');
        card.className = 'chat-product-card';
        const image = document.createElement('img');
        image.src = imageUrl(product.id, product.image);
        image.alt = product.name;
        image.loading = 'lazy';
        const details = document.createElement('div');
        details.className = 'chat-product-details';
        const name = document.createElement('strong');
        name.textContent = product.name;
        const meta = document.createElement('small');
        meta.textContent = `${product.color} · ${money.format(product.price)} · Còn ${product.stock}`;
        const actions = document.createElement('div');
        actions.className = 'chat-product-actions';
        const size = document.createElement('select');
        size.className = 'chat-product-size';
        size.setAttribute('aria-label', `Chọn size cho ${product.name}`);
        for (const value of product.sizes) {
            const option = document.createElement('option');
            option.value = value;
            option.textContent = value;
            size.append(option);
        }
        const view = document.createElement('button');
        view.type = 'button';
        view.className = 'chat-product-view';
        view.dataset.product = product.id;
        view.textContent = 'Chi tiết';
        const add = document.createElement('button');
        add.type = 'button';
        add.className = 'chat-product-add';
        add.dataset.addProduct = product.id;
        add.textContent = 'Thêm vào túi';
        actions.append(size, view, add);
        details.append(name, meta, actions);
        card.append(image, details);
        cards.append(card);
    }
    bubble.after(cards);
};
const renderAiChatHistory = () => {
    const host = document.getElementById('chat-messages');
    if (!chatHistory.length) return;
    host.replaceChildren();
    for (const message of chatHistory) appendChatMessage(message.role, message.content, false, message.productCodes);
};
document.getElementById('chat-form').addEventListener('submit', async event => {
    event.preventDefault();
    const input = document.getElementById('chat-input');
    const text = input.value.trim();
    if (!text) return;

    if (supportConversationId) {
        await sendSupportMessage(text, event.currentTarget);
        return;
    }

    if (isCustomer && isHumanSupportRequest(text)) {
        await startSupportChat(text, event.currentTarget);
        return;
    }

    input.value = '';
    chatHistory.push({ role: 'user', content: text });
    saveChatHistory();
    appendChatMessage('user', text);
    const pending = appendChatMessage('assistant', 'Stylist đang suy nghĩ…', true);
    const submitButton = event.currentTarget.querySelector('button');
    submitButton.disabled = true;
    try {
        const response = await fetch('/api/chat', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]').value
            },
            body: JSON.stringify({ messages: chatHistory.slice(-12) })
        });
        const result = await response.json().catch(() => ({}));
        if (response.status === 401) {
            redirectToLoginFromStore();
            return;
        }
        if (!response.ok) throw new Error(result.error || `Không thể gửi tin nhắn (HTTP ${response.status}).`);
        if (typeof result.reply !== 'string' || !result.reply.trim()) throw new Error('Stylist chưa trả về câu trả lời. Vui lòng thử lại.');
        const productCodes = Array.isArray(result.productCodes)
            ? result.productCodes.filter(code => typeof code === 'string' && products.some(product => product.id === code && product.stock > 0))
            : [];
        pending.textContent = result.reply;
        pending.classList.remove('is-pending');
        renderRecommendationCards(productCodes, pending);
        chatHistory.push({ role: 'assistant', content: result.reply, productCodes });
        saveChatHistory();
    } catch (error) {
        pending.textContent = error instanceof TypeError ? 'Không kết nối được máy chủ. Hãy kiểm tra ứng dụng đang chạy rồi thử lại.' : error.message;
        pending.classList.remove('is-pending');
        pending.classList.add('chat-error');
    } finally {
        submitButton.disabled = false;
        input.focus();
    }
});

const supportToken = () => document.querySelector('#antiforgery-form input[name="__RequestVerificationToken"]').value;
const supportStatus = document.getElementById('human-chat-status');
let supportConversationClosed = false;
const isHumanSupportRequest = text => {
    const normalized = text.toLocaleLowerCase('vi').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
    const mentionsEmployee = /\b(nhan vien|tu van vien|nhan vien tu van|nguoi that)\b/.test(normalized);
    const asksToTransfer = /\b(chuyen|chat|noi chuyen|gap|tu van|ket noi|noi voi|can|goi|ho tro)\b/.test(normalized);
    return mentionsEmployee && asksToTransfer;
};
const setHumanChatMode = conversation => {
    supportConversationId = conversation.id;
    supportConversationClosed = false;
    document.getElementById('stylist-shortcuts').hidden = true;
    supportStatus.hidden = false;
    supportStatus.textContent = conversation.assignedEmployeeName
        ? `Bạn đang trò chuyện với ${conversation.assignedEmployeeName}.`
        : 'Đã chuyển yêu cầu. Nhân viên sẽ tham gia ngay khi sẵn sàng.';
    document.querySelector('.chat-header strong').textContent = conversation.assignedEmployeeName || 'Đang chờ nhân viên';
    document.getElementById('chat-subtitle').textContent = 'Cuộc trò chuyện với nhân viên hỗ trợ';
    document.getElementById('chat-input').placeholder = 'Nhắn tin...';
    document.querySelector('.chat-disclaimer').textContent = 'Không chia sẻ mật khẩu hoặc thông tin thanh toán.';
};
const startSupportChat = async (request, form) => {
    const submitButton = form.querySelector('button');
    submitButton.disabled = true;
    supportStatus.hidden = false;
    supportStatus.textContent = 'Đang kết nối với nhân viên...';
    try {
        const transcript = [...chatHistory, { role: 'user', content: request }];
        const response = await fetch('/api/support-chat/start', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': supportToken(),
                'X-Client-Session': tabSessionId
            },
            body: JSON.stringify({ messages: transcript })
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.error || `Không thể chuyển cuộc trò chuyện (HTTP ${response.status}).`);
        document.getElementById('chat-input').value = '';
        setHumanChatMode(result.conversation);
        await pollSupportMessages(true);
        startSupportPolling();
    } catch (error) {
        supportStatus.hidden = true;
        showToast(error.message);
    } finally {
        submitButton.disabled = false;
        document.getElementById('chat-input').focus();
    }
};
const sendSupportMessage = async (content, form) => {
    if (supportConversationClosed) return;
    const input = document.getElementById('chat-input');
    const button = form.querySelector('button');
    button.disabled = true;
    try {
        const response = await fetch(`/api/support-chat/${supportConversationId}/messages`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': supportToken() },
            body: JSON.stringify({ content })
        });
        const result = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(result.error || `Không thể gửi tin nhắn (HTTP ${response.status}).`);
        input.value = '';
        supportAfterMessageId = Math.max(supportAfterMessageId, result.message.id);
        appendChatMessage('user', result.message.content);
    } catch (error) {
        showToast(error.message);
    } finally {
        button.disabled = false;
        input.focus();
    }
};
const renderSupportMessages = messages => {
    const host = document.getElementById('chat-messages');
    host.replaceChildren();
    for (const message of messages) {
        supportAfterMessageId = Math.max(supportAfterMessageId, message.id);
        appendChatMessage(message.senderRole === 'Customer' ? 'user' : 'assistant', message.content);
    }
};
const pollSupportMessages = async initial => {
    if (!supportConversationId) return;
    const response = await fetch(`/api/support-chat/${supportConversationId}/messages?after=${initial ? 0 : supportAfterMessageId}`);
    const result = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(result.error || `Không thể tải tin nhắn hỗ trợ (HTTP ${response.status}).`);
    if (initial) renderSupportMessages(result.messages || []);
    else for (const message of result.messages || []) {
        supportAfterMessageId = Math.max(supportAfterMessageId, message.id);
        appendChatMessage(message.senderRole === 'Customer' ? 'user' : 'assistant', message.content);
    }
    if (result.status === 'Closed') {
        supportConversationClosed = true;
        const input = document.getElementById('chat-input');
        input.disabled = true;
        input.placeholder = 'Cuộc trò chuyện đã kết thúc.';
        supportStatus.textContent = 'Cuộc trò chuyện đã kết thúc.';
        clearInterval(supportPollTimer);
    }
};
const startSupportPolling = () => {
    clearInterval(supportPollTimer);
    supportPollTimer = setInterval(() => pollSupportMessages(false).catch(error => {
        supportStatus.textContent = error.message;
    }), 3000);
};
const loadCurrentSupportChat = async () => {
    if (supportLoaded || !isCustomer) return;
    supportLoaded = true;
    try {
        const response = await fetch('/api/support-chat/current', {
            headers: { 'X-Client-Session': tabSessionId }
        });
        if (!response.ok) throw new Error(`Không thể tải cuộc trò chuyện hiện tại (HTTP ${response.status}).`);
        const result = await response.json();
        if (!result.conversation) return;
        setHumanChatMode(result.conversation);
        await pollSupportMessages(true);
        startSupportPolling();
    } catch (error) {
        supportLoaded = false;
        showToast(error.message);
    }
};

if ('IntersectionObserver' in window && !window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
    const revealTargets = document.querySelectorAll([
        '.hero-copy > .section-kicker',
        '.hero-copy > h1',
        '.hero-copy > p',
        '.hero-copy > .text-link',
        '.season-content > .section-kicker',
        '.season-content > h2',
        '.season-content > .season-description',
        '.season-content > .season-picker',
        '.season-content > .season-note',
        '.season-content > .season-recommendation-heading > div > *',
        '.color-stylist-intro > *',
        '.color-model-heading',
        '.color-guide-card > .section-kicker',
        '.color-guide-card > h3',
        '.combination-heading',
        '.matching-products-heading',
        '.collection-heading-row > div > *',
        '.category-tabs',
        '.catalog-filters',
        '.stylist-banner > div > *',
        '.stylist-banner > .outline-button'
    ].join(','));
    scrollRevealObserver = new IntersectionObserver(entries => {
        entries.forEach(entry => entry.target.classList.toggle('is-scroll-visible', entry.isIntersecting));
    }, { threshold: 0.12 });

    revealTargets.forEach((element, index) => {
        element.classList.add('scroll-reveal');
        element.style.setProperty('--scroll-reveal-offset', index % 2 === 0 ? '-48px' : '48px');
        element.style.setProperty('--scroll-reveal-delay', `${index % 4 * 65}ms`);
        scrollRevealObserver.observe(element);
    });
}
