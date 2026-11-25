(() => {
  const story = document.querySelector('.story');
  const screen = document.querySelector('.screen');
  const wrap = document.querySelector('.island-wrap');
  const count = document.getElementById('count');
  const STATES = 5;
  const DESIGN_WIDTH = 620; // screen width at which the island is drawn 1:1

  // Pick the story state from how far the sticky section has been scrolled.
  let current = -1;
  function onScroll() {
    const r = story.getBoundingClientRect();
    const travel = r.height - window.innerHeight;
    const p = Math.min(1, Math.max(0, -r.top / travel));
    story.style.setProperty('--p', p.toFixed(4));
    const i = Math.min(STATES - 1, Math.floor(p * STATES));
    if (i !== current) {
      current = i;
      story.dataset.state = String(i);
      count.textContent = String(i + 1).padStart(2, '0');
    }
  }

  // Shrink the island on small screens so it keeps the same proportion.
  function onResize() {
    const s = Math.min(1, screen.clientWidth / DESIGN_WIDTH);
    wrap.style.setProperty('--s', s.toFixed(3));
    onScroll();
  }

  let ticking = false;
  window.addEventListener('scroll', () => {
    if (!ticking) {
      ticking = true;
      requestAnimationFrame(() => { onScroll(); ticking = false; });
    }
  }, { passive: true });
  window.addEventListener('resize', onResize);
  onResize();

  // Navigation island: hover opens it on desktop; tap toggles it on touch screens.
  const isle = document.getElementById('isle');
  const head = isle.querySelector('.isle-head');
  head.addEventListener('click', () => {
    const open = !isle.classList.contains('open');
    isle.classList.toggle('open', open);
    head.setAttribute('aria-expanded', String(open));
  });
  isle.querySelectorAll('.isle-links a').forEach((a) => a.addEventListener('click', () => {
    isle.classList.remove('open');
    head.setAttribute('aria-expanded', 'false');
  }));
  document.addEventListener('click', (e) => {
    if (!isle.contains(e.target)) { isle.classList.remove('open'); head.setAttribute('aria-expanded', 'false'); }
  });

  // Real time in the clock view.
  const clock = document.getElementById('clock');
  const date = document.getElementById('date');
  const idleTime = document.getElementById('idle-time');
  const idleDate = document.getElementById('idle-date');
  const WEEK = ['日', '一', '二', '三', '四', '五', '六'];
  function tick() {
    const d = new Date();
    clock.textContent = `${String(d.getHours()).padStart(2, '0')}:${String(d.getMinutes()).padStart(2, '0')}`;
    date.textContent = `${d.getMonth() + 1}月${d.getDate()}日 星期${WEEK[d.getDay()]}`;
    idleTime.textContent = clock.textContent;
    idleDate.textContent = `${d.getMonth() + 1}/${d.getDate()} 週${WEEK[d.getDay()]}`;
    setTimeout(tick, (60 - d.getSeconds()) * 1000);
  }
  tick();

  // Point the download button straight at the latest installer and show its version.
  // If GitHub can't be reached, the button keeps linking to the Releases page.
  const dlBtn = document.getElementById('dl-btn');
  const dlVer = document.getElementById('dl-ver');
  const dlSha = document.getElementById('dl-sha');
  fetch('https://api.github.com/repos/erich-5245/island/releases/latest', { headers: { Accept: 'application/vnd.github+json' } })
    .then((r) => (r.ok ? r.json() : Promise.reject(r.status)))
    .then((rel) => {
      const exe = rel.assets.find((a) => /\.exe$/i.test(a.name));
      if (!exe) return;
      dlBtn.href = exe.browser_download_url;
      const mb = Math.max(1, Math.round(exe.size / 1048576));
      const d = new Date(rel.published_at);
      dlVer.textContent = `${rel.tag_name} · ${mb} MB · ${d.getFullYear()}/${d.getMonth() + 1}/${d.getDate()} 發布 · `;
      const sha = rel.assets.find((a) => /\.sha256$/i.test(a.name));
      if (sha) dlSha.href = sha.browser_download_url;
    })
    .catch(() => {});

  // Fade sections in as they enter the viewport.
  const io = new IntersectionObserver((entries) => {
    for (const e of entries) {
      if (e.isIntersecting) { e.target.classList.add('in'); io.unobserve(e.target); }
    }
  }, { threshold: 0.15 });
  document.querySelectorAll('.reveal').forEach((el) => io.observe(el));
})();
