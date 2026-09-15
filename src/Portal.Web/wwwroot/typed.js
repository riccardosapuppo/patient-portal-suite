/*
    The typed sign-in, put behind a button.

    The markup ships the form on the page, inside a dialog that is already
    open, with the button that would reveal it hidden. This script does it the
    other way round: it closes the dialog and shows the button. So the order of
    events is that the form works first and is tidied away second, and a
    browser that never runs this is left with a page that works rather than a
    button that does nothing.
*/

(() => {
  const typed = document.querySelector('.typed');
  const open = document.querySelector('.typed-open');
  if (!typed || !open || typeof typed.showModal !== 'function') return;

  typed.removeAttribute('open');
  open.hidden = false;

  open.addEventListener('click', () => {
    typed.showModal();

    // The select, not the dialog: somebody who asked to type it has said what
    // they want to do, and the first thing to do is choose a patient.
    typed.querySelector('select')?.focus();
  });

  typed.querySelector('.typed-shut').addEventListener('click', () => typed.close());
})();
