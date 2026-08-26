(function () {
    var categoryNode = document.getElementById("category-chart-data");
    var stockNode = document.getElementById("stock-chart-data");
    var categoryCanvas = document.getElementById("categoryChart");
    var stockCanvas = document.getElementById("stockChart");

    function parseJson(node) {
        if (!node) {
            return null;
        }
        var text = (node.textContent || "").trim();
        if (!text) {
            return null;
        }
        try {
            return JSON.parse(text);
        } catch (error) {
            return null;
        }
    }

    function hasChartJs() {
        return typeof window.Chart !== "undefined";
    }

    var palette = ["#f5a524", "#3dd6c6", "#60a5fa", "#a78bfa", "#fb7185", "#f8d57a", "#38bdf8", "#c4b5fd"];

    var categoryData = parseJson(categoryNode);
    if (hasChartJs() && categoryCanvas && categoryData && categoryData.labels && categoryData.labels.length) {
        new window.Chart(categoryCanvas, {
            type: "bar",
            data: {
                labels: categoryData.labels,
                datasets: [{
                    label: "ยอดขาย (บาท)",
                    data: categoryData.values,
                    backgroundColor: palette,
                    borderRadius: 10,
                    maxBarThickness: 42
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    x: {
                        ticks: { color: "#9aa7bd" },
                        grid: { display: false }
                    },
                    y: {
                        ticks: { color: "#9aa7bd" },
                        grid: { color: "rgba(154,167,189,0.12)" }
                    }
                }
            }
        });
    }

    var stockData = parseJson(stockNode);
    if (hasChartJs() && stockCanvas && stockData && stockData.labels && stockData.labels.length) {
        new window.Chart(stockCanvas, {
            type: "doughnut",
            data: {
                labels: stockData.labels,
                datasets: [{
                    data: stockData.values,
                    backgroundColor: palette,
                    borderWidth: 0
                }]
            },
            options: {
                responsive: true,
                plugins: {
                    legend: {
                        position: "bottom",
                        labels: { color: "#f6f1e8", boxWidth: 12, padding: 16 }
                    }
                },
                cutout: "62%"
            }
        });
    }

    var links = document.querySelectorAll(".side-nav a");
    links.forEach(function (link) {
        link.addEventListener("click", function () {
            links.forEach(function (item) {
                item.classList.remove("is-active");
            });
            link.classList.add("is-active");
        });
    });
})();
