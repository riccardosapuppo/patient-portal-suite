/*
    Opening a report without losing the list it was on.

    Pressing Open navigated away from the page, handed over a PDF, and left
    somebody in a browser's document viewer with no way back but the back
    button -- which, on a portal, is the one button people have been taught not
    to trust. The list is the place you work from; a document should arrive on
    top of it and go away again.

    Enhancement, not machinery. Every Open is a real link to the real document
    and it opens in a new tab on its own; this intercepts the click and shows
    the same URL in a dialog instead. Turn this file off and the portal still
    hands over every report -- the list simply stays behind in the other tab.

    <dialog> rather than a div pretending to be one: Escape closes it, the page
    behind is inert while it is open, and focus is handled by the browser
    instead of by a hundred lines here that get it nearly right.
*/

(() => {
  const viewer = document.querySelector('.viewer');
  if (!viewer || typeof viewer.showModal !== 'function') return;

  const frame = viewer.querySelector('.viewer-page');
  const what = viewer.querySelector('.viewer-what');
  const which = viewer.querySelector('.viewer-id');
  const tab = viewer.querySelector('.viewer-tab');

  const shut = () => {
    // Emptied on the way out. A report left loaded in a hidden frame is a
    // report still on the screen as far as the next person is concerned.
    frame.removeAttribute('src');
    if (viewer.open) viewer.close();
  };

  document.querySelectorAll('a[data-open]').forEach((link) => {
    link.addEventListener('click', (pressed) => {
      // Anything but a plain left click is somebody asking for a tab on
      // purpose, and they are to have one.
      if (pressed.defaultPrevented || pressed.button !== 0) return;
      if (pressed.metaKey || pressed.ctrlKey || pressed.shiftKey || pressed.altKey) return;

      pressed.preventDefault();

      what.textContent = link.dataset.what || 'Report';
      which.textContent = link.dataset.open;
      tab.href = link.href;

      // The fragment is for the browser's own PDF viewer, not for the portal,
      // and it turns off the second toolbar it would otherwise draw under
      // ours. Only that: the zoom hints in the same family are advisory and
      // this viewer ignores them, so the sheet is made wide enough for a page
      // at its own size instead of asking. A fragment costs nothing where it
      // is not understood -- the server never sees it -- so the frame gets one
      // and the new tab does not.
      frame.src = `${link.href}#toolbar=0&navpanes=0`;

      viewer.showModal();
    });
  });

  viewer.querySelector('.viewer-shut').addEventListener('click', shut);
  viewer.addEventListener('cancel', (escaped) => {
    escaped.preventDefault();
    shut();
  });

  // The dark ground outside the sheet. A dialog's backdrop is part of the
  // dialog, so a press anywhere lands on it and only the ones outside the
  // sheet's own box count.
  viewer.addEventListener('click', (pressed) => {
    if (pressed.target !== viewer) return;

    const box = viewer.getBoundingClientRect();
    const inside =
      pressed.clientX >= box.left &&
      pressed.clientX <= box.right &&
      pressed.clientY >= box.top &&
      pressed.clientY <= box.bottom;

    if (!inside) shut();
  });
})();
