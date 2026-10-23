// Повноекранний перегляд діаграм Mermaid: кнопка над кожною діаграмою відкриває її в оверлеї,
// де працюють зум колесом, перетягування мишею чи пальцем і кнопки +/−/скинути. Esc закриває.
// Без залежностей: сторінка лишається статичною, а SVG у самому тексті не чіпається.
(() => {
	const MIN = 0.5;
	const MAX = 6;

	const overlay = document.createElement('div');
	overlay.className = 'diagram-viewer';
	overlay.hidden = true;
	overlay.innerHTML = `
		<div class="diagram-viewer__bar">
			<span class="diagram-viewer__hint">Колесо — масштаб, перетягування — рух</span>
			<div class="diagram-viewer__actions">
				<button type="button" data-act="out" aria-label="Зменшити">−</button>
				<button type="button" data-act="reset" aria-label="Скинути масштаб">100%</button>
				<button type="button" data-act="in" aria-label="Збільшити">+</button>
				<button type="button" data-act="close" aria-label="Закрити">✕</button>
			</div>
		</div>
		<div class="diagram-viewer__stage"><div class="diagram-viewer__canvas mermaid"></div></div>`;
	document.body.append(overlay);

	// The canvas carries the «mermaid» class so the brand colours from brand.css reach the clone too.
	const stage = overlay.querySelector('.diagram-viewer__stage');
	const canvas = overlay.querySelector('.diagram-viewer__canvas');
	const resetButton = overlay.querySelector('[data-act="reset"]');

	let scale = 1;
	let x = 0;
	let y = 0;

	const apply = () => {
		canvas.style.transform = `translate(${x}px, ${y}px) scale(${scale})`;
		resetButton.textContent = `${Math.round(scale * 100)}%`;
	};

	const zoomAt = (factor, clientX, clientY) => {
		const next = Math.min(MAX, Math.max(MIN, scale * factor));
		const rect = stage.getBoundingClientRect();
		const px = clientX - rect.left - x;
		const py = clientY - rect.top - y;
		x -= px * (next / scale - 1);
		y -= py * (next / scale - 1);
		scale = next;
		apply();
	};

	const fit = () => {
		const svg = canvas.querySelector('svg');
		if (!svg) {
			return;
		}
		const box = svg.getBoundingClientRect();
		const stageBox = stage.getBoundingClientRect();
		const natural = { w: box.width / scale, h: box.height / scale };
		scale = Math.min(1.6, (stageBox.width - 48) / natural.w, (stageBox.height - 48) / natural.h);
		x = (stageBox.width - natural.w * scale) / 2;
		y = (stageBox.height - natural.h * scale) / 2;
		apply();
	};

	const open = (source) => {
		canvas.replaceChildren(source.cloneNode(true));
		// Mermaid gives the SVG width="100%" and an inline max-width; in a free-floating canvas that
		// collapses to nothing, so the clone gets its natural size from the viewBox instead.
		const svg = canvas.querySelector('svg');
		const box = svg.viewBox.baseVal;
		const natural = box?.width ? { w: box.width, h: box.height } : source.getBoundingClientRect();
		svg.removeAttribute('width');
		svg.removeAttribute('height');
		svg.style.maxWidth = 'none';
		svg.style.width = `${natural.w || natural.width}px`;
		svg.style.height = `${natural.h || natural.height}px`;
		overlay.hidden = false;
		document.body.style.overflow = 'hidden';
		scale = 1;
		x = 0;
		y = 0;
		apply();
		requestAnimationFrame(fit);
		overlay.querySelector('[data-act="close"]').focus();
	};

	const close = () => {
		overlay.hidden = true;
		document.body.style.overflow = '';
		canvas.replaceChildren();
	};

	overlay.addEventListener('click', (event) => {
		const act = event.target.closest('[data-act]')?.dataset.act;
		const center = () => {
			const rect = stage.getBoundingClientRect();
			return [rect.left + rect.width / 2, rect.top + rect.height / 2];
		};
		if (act === 'close') close();
		if (act === 'in') zoomAt(1.25, ...center());
		if (act === 'out') zoomAt(0.8, ...center());
		if (act === 'reset') fit();
	});

	document.addEventListener('keydown', (event) => {
		if (!overlay.hidden && event.key === 'Escape') {
			close();
		}
	});

	stage.addEventListener('wheel', (event) => {
		event.preventDefault();
		zoomAt(event.deltaY < 0 ? 1.1 : 0.9, event.clientX, event.clientY);
	}, { passive: false });

	// Drag with a mouse or one finger; pinch with two fingers.
	const pointers = new Map();
	let pinchStart = null;
	stage.addEventListener('pointerdown', (event) => {
		stage.setPointerCapture(event.pointerId);
		pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
		if (pointers.size === 2) {
			const [a, b] = [...pointers.values()];
			pinchStart = { distance: Math.hypot(a.x - b.x, a.y - b.y), scale };
		}
		stage.classList.add('is-dragging');
	});
	stage.addEventListener('pointermove', (event) => {
		const previous = pointers.get(event.pointerId);
		if (!previous) {
			return;
		}
		pointers.set(event.pointerId, { x: event.clientX, y: event.clientY });
		if (pointers.size === 2 && pinchStart) {
			const [a, b] = [...pointers.values()];
			const distance = Math.hypot(a.x - b.x, a.y - b.y);
			const factor = (pinchStart.scale * distance / pinchStart.distance) / scale;
			zoomAt(factor, (a.x + b.x) / 2, (a.y + b.y) / 2);
			return;
		}
		x += event.clientX - previous.x;
		y += event.clientY - previous.y;
		apply();
	});
	const release = (event) => {
		pointers.delete(event.pointerId);
		if (pointers.size < 2) {
			pinchStart = null;
		}
		if (pointers.size === 0) {
			stage.classList.remove('is-dragging');
		}
	};
	stage.addEventListener('pointerup', release);
	stage.addEventListener('pointercancel', release);

	// A button per diagram, added once Mermaid has drawn it (astro-mermaid renders on the client).
	const decorate = (pre) => {
		if (pre.dataset.viewer || !pre.querySelector('svg')) {
			return;
		}
		pre.dataset.viewer = 'ready';
		const button = document.createElement('button');
		button.type = 'button';
		button.className = 'diagram-viewer__open';
		button.textContent = 'На весь екран';
		button.addEventListener('click', () => open(pre.querySelector('svg')));
		const wrap = document.createElement('div');
		wrap.className = 'diagram-viewer__host';
		pre.replaceWith(wrap);
		wrap.append(button, pre);
	};

	const scan = () => document.querySelectorAll('pre.mermaid[data-processed]').forEach(decorate);
	scan();
	new MutationObserver(scan).observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['data-processed'] });
})();
