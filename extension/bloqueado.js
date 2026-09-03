const params = new URLSearchParams(window.location.search);
document.getElementById("dominio").textContent = params.get("dominio") || "";

document.getElementById("fechar").addEventListener("click", () => {
  window.close();
});
