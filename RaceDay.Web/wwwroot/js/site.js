(function () {
    // Mobile menu
    var toggle = document.querySelector('[data-nav-toggle]');
    var nav = document.querySelector('[data-nav]');
    if (toggle && nav) toggle.addEventListener('click', function () { nav.classList.toggle('open'); });

    // Confirm before destructive actions: <form data-confirm="Delete this?">
    document.querySelectorAll('form[data-confirm]').forEach(function (f) {
        f.addEventListener('submit', function (e) {
            if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault();
        });
    });

    // Registration: only participants need a date of birth
    var roleInputs = document.querySelectorAll('input[name="Role"]');
    var dobBlock = document.querySelector('[data-participant-only]');
    function syncRole() {
        if (!dobBlock || !roleInputs.length) return;
        var selected = document.querySelector('input[name="Role"]:checked');
        dobBlock.style.display = selected && selected.value === 'Organiser' ? 'none' : '';
    }
    roleInputs.forEach(function (r) { r.addEventListener('change', syncRole); });
    syncRole();
})();
