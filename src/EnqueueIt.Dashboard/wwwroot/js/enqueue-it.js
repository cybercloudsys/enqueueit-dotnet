function selectedIds() {
    return $.map($('[name="chkJobId"]:checked'), e => $(e).data('id'));
}

function stopSelectedJobs() {
    $.ajax({ url: './StopJobs', type: 'POST', dataType: "json", data: { ids: selectedIds() },
        xhrFields: { withCredentials: true }, contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        success: function () {
            location.reload();
        },
        error: function () {
            alert("somwthing went wrong!");
        }
    });
}


function deleteScheduledJobs() {
    $.ajax({ url: './DeleteScheduledJobs', type: 'POST', dataType: "json", data: { ids: selectedIds() },
        xhrFields: { withCredentials: true }, contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        success: function () {
            location.reload();
        },
        error: function () {
            alert("somwthing went wrong!");
        }
    });
}

function deleteSelectedJobs(status) {
    $.ajax({ url: './DeleteJobs', type: 'POST', dataType: "json", data: { ids: selectedIds(), status: status },
        xhrFields: { withCredentials: true }, contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        success: function () {
            location.reload();
        },
        error: function () {
            alert("somwthing went wrong!");
        }
    });
}

function retrySelectedJobs() {
    $.ajax({ url: './RetryJobs', type: 'POST', dataType: "json", data: { ids: selectedIds() },
        xhrFields: { withCredentials: true }, contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        success: function () {
            location.reload();             
        },
        error: function () {
            alert("somwthing went wrong!");
        }
    });
}

function enqueueSelectedJobs() {
    $.ajax({ url: './EnqueueJobs', type: 'POST', dataType: "json", data: { ids: selectedIds() },
        xhrFields: { withCredentials: true }, contentType: 'application/x-www-form-urlencoded; charset=UTF-8',
        success: function () {
            location.reload();             
        },
        error: function () {
            alert("somwthing went wrong!");
        }
    });
}

function selectAll(checked) {
    $('[name="chkJobId"][type="checkbox"]').each((i, e) => { e.checked = checked; });
}

function searchEnter(e) {
    if (e.keyCode == 13) {
        e.preventDefault();
        search($('#search-text').val());
    }
}

function search(text) {
    document.location = $('#search-btn').data('url') + (!text ? '' : ('&search=' + text));
}

function setLoadTime() {
    $('span, i').tooltip();
    setTimeout(loadTime(), 3000);
}

function loadTime() {
    for (var i = 0; i < $('.dateTime').length; i++) {
        var item = $('.dateTime').eq(i);
        item.text(moment.utc(item.data('time')).fromNow());
    }
    setTimeout(() => loadTime(), 1000);
}

const getOrCreateTooltip = (chart) => {
    let tooltipEl = chart.canvas.parentNode.querySelector('div');
  
    if (!tooltipEl) {
        tooltipEl = document.createElement('div');
        tooltipEl.style.background = 'rgba(0, 0, 0, 0.7)';
        tooltipEl.style.borderRadius = '3px';
        tooltipEl.style.color = 'white';
        tooltipEl.style.opacity = 0.8;
        tooltipEl.style.pointerEvents = 'none';
        tooltipEl.style.position = 'absolute';
        tooltipEl.style.zIndex = 1;
        tooltipEl.style.transform = 'translate(-50%, 0)';
        tooltipEl.style.transition = 'all .1s ease';

        const table = document.createElement('table');
        table.style.margin = '0px';

        tooltipEl.appendChild(table);
        chart.canvas.parentNode.appendChild(tooltipEl);
    }
  
    return tooltipEl;
};
  
const externalTooltipHandler = (context) => {
    // Tooltip Element
    const {chart, tooltip} = context;
    const tooltipEl = getOrCreateTooltip(chart);
  
    // Hide if no tooltip
    if (tooltip.opacity === 0) {
        tooltipEl.style.opacity = 0;
        return;
    }

    // Set Text
    if (tooltip.body) {
        const titleLines = tooltip.title || [];
        const bodyLines = tooltip.body.map(b => b.lines);

        const tableHead = document.createElement('thead');

        titleLines.forEach(title => {
        const tr = document.createElement('tr');
        tr.style.borderWidth = 0;

        const th = document.createElement('th');
        th.style.borderWidth = 0;
        const text = document.createTextNode(title);

        th.appendChild(text);
        tr.appendChild(th);
        tableHead.appendChild(tr);
        });

        const tableBody = document.createElement('tbody');
        bodyLines.forEach((body, i) => {
            const colors = tooltip.labelColors[i];

            const span = document.createElement('span');
            span.style.background = colors.backgroundColor;
            span.style.borderColor = colors.borderColor;
            span.style.borderWidth = '2px';
            span.style.marginRight = '10px';
            span.style.height = '10px';
            span.style.width = '10px';
            span.style.display = 'inline-block';

            const tr = document.createElement('tr');
            tr.style.backgroundColor = 'inherit';
            tr.style.borderWidth = 0;

            const td = document.createElement('td');
            td.style.borderWidth = 0;

            if(chart.config._config.id == 'performance')
                body += i == 0 ? ' MB' : '%';

            const text = document.createTextNode(body);

            td.appendChild(span);
            td.appendChild(text);
            tr.appendChild(td);
            tableBody.appendChild(tr);
        });

        const tableRoot = tooltipEl.querySelector('table');

        // Remove old children
        while (tableRoot.firstChild) {
            tableRoot.firstChild.remove();
        }

        // Add new children
        tableRoot.appendChild(tableHead);
        tableRoot.appendChild(tableBody);
    }
  
    const {offsetLeft: positionX, offsetTop: positionY} = chart.canvas;
  
    // Display, position, and set styles for font
    tooltipEl.style.opacity = 0.8;
    tooltipEl.style.left = positionX + tooltip.caretX + 'px';
    tooltipEl.style.top = positionY + tooltip.caretY + 'px';
    tooltipEl.style.font = tooltip.options.bodyFont.string;
    tooltipEl.style.padding = tooltip.options.padding + 'px ' + tooltip.options.padding + 'px';
};