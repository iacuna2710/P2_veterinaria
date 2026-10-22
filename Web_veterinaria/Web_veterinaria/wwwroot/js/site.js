document.addEventListener("DOMContentLoaded", () => {
    const summary = document.querySelector(".validation-summary-errors");
    if (summary) summary.focus();
    document.querySelectorAll(".input-validation-error").forEach(field => field.setAttribute("aria-invalid", "true"));
});

$("form").on("invalid-form", (_, validator) => {
    validator.errorList.forEach(error => error.element.setAttribute("aria-invalid", "true"));
});
