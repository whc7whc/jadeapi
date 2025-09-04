// Set new default font family and font color to mimic Bootstrap's default styling
if (typeof Chart !== 'undefined') {
    Chart.defaults.font.family = 'Nunito, -apple-system, system-ui, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif';
    Chart.defaults.color = '#858796';
}

// Pie Chart Example - 檢查元素是否存在
document.addEventListener('DOMContentLoaded', function() {
    var ctx = document.getElementById("myPieChart");
    if (ctx && typeof Chart !== 'undefined') {
      try {
        var myPieChart = new Chart(ctx, {
          type: 'doughnut',
          data: {
            labels: ["Direct", "Referral", "Social"],
            datasets: [{
              data: [55, 30, 15],
              backgroundColor: ['#4e73df', '#1cc88a', '#36b9cc'],
              hoverBackgroundColor: ['#2e59d9', '#17a673', '#2c9faf'],
              hoverBorderColor: "rgba(234, 236, 244, 1)",
            }],
          },
          options: {
            maintainAspectRatio: false,
            plugins: {
              tooltip: {
                backgroundColor: "rgb(255,255,255)",
                bodyColor: "#858796",
                borderColor: '#dddfeb',
                borderWidth: 1,
                padding: 15,
                displayColors: false,
                caretPadding: 10,
              },
              legend: {
                display: false
              }
            },
            cutout: '80%'
          },
        });
      } catch (error) {
        console.warn('Chart.js Pie Chart initialization failed:', error);
      }
    }
});
