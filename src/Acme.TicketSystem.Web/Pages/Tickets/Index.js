(function () {
    $(function () {
        var l = abp.localization.getResource('TicketSystem');

        // 票种目录：单价、名称都从服务端拿，页面只读不填
        var prices = [];

        function escapeHtml(value) {
            return $('<div>').text(value === null || value === undefined ? '' : value).html();
        }

        function findPrice(code) {
            for (var i = 0; i < prices.length; i++) {
                if (prices[i].ticketCode === code) {
                    return prices[i];
                }
            }

            return null;
        }

        // 一个票种一行：票种（下拉）、名称（只读）、单价（只读）、数量（可填），小计自动算
        function createRow() {
            var $row = $(
                '<tr>' +
                '<td><select class="form-select form-select-sm line-code"></select></td>' +
                '<td class="line-name"></td>' +
                '<td class="line-price text-end"></td>' +
                '<td><input type="number" class="form-control form-control-sm line-qty" min="1" step="1" value="1" /></td>' +
                '<td class="line-amount text-end">0.00</td>' +
                '<td class="text-end">' +
                '<button type="button" class="btn btn-sm btn-outline-danger remove-line" title="' + l('Delete') + '">' +
                '<i class="fa fa-trash"></i></button>' +
                '</td>' +
                '</tr>'
            );

            fillOptions($row);
            return $row;
        }

        function fillOptions($row) {
            var $select = $row.find('.line-code');

            $select.empty();
            $select.append($('<option></option>').attr('value', '').text(l('SelectTicketType')));

            prices.forEach(function (price) {
                $select.append(
                    $('<option></option>')
                        .attr('value', price.ticketCode)
                        .attr('data-name', price.ticketName)
                        .attr('data-price', price.unitPrice)
                        .text(price.ticketName + ' (' + price.ticketCode + ')')
                );
            });
        }

        // 选中票种后，把名称和目录单价显示出来（只读）
        function syncRow($row) {
            var code = $row.find('.line-code').val();
            var price = code ? findPrice(code) : null;

            $row.find('.line-name').text(price ? price.ticketName : '');
            $row.find('.line-price').text(price ? Number(price.unitPrice).toFixed(2) : '');

            recalc();
        }

        function recalc() {
            var total = 0;

            $('#LinesTable tbody tr').each(function () {
                var $row = $(this);
                var code = $row.find('.line-code').val();
                var price = code ? findPrice(code) : null;

                if (!price) {
                    $row.find('.line-amount').text('0.00');
                    return;
                }

                var quantity = parseInt($row.find('.line-qty').val(), 10) || 0;
                var amount = price.unitPrice * quantity;

                $row.find('.line-amount').text(amount.toFixed(2));
                total += amount;
            });

            $('#TotalAmount').text(total.toFixed(2));
            return total;
        }

        function resetRows() {
            $('#LinesTable tbody').empty();
            addRow();
            recalc();
        }

        function addRow() {
            var $row = createRow();
            $('#LinesTable tbody').append($row);
            return $row;
        }

        $('#AddLineButton').click(function (e) {
            e.preventDefault();
            addRow();
        });

        $('#LinesTable').on('click', '.remove-line', function (e) {
            e.preventDefault();
            $(this).closest('tr').remove();

            if ($('#LinesTable tbody tr').length === 0) {
                addRow();
            }

            recalc();
        });

        $('#LinesTable').on('change', '.line-code', function (e) {
            syncRow($(this).closest('tr'));
        });

        $('#LinesTable').on('input change', '.line-qty', recalc);

        function collectInput() {
            var lines = [];

            $('#LinesTable tbody tr').each(function () {
                var $row = $(this);
                var code = $row.find('.line-code').val();
                var quantity = parseInt($row.find('.line-qty').val(), 10) || 0;

                // 没选票种的空行直接跳过
                if (!code) {
                    return;
                }

                lines.push({
                    ticketCode: code,
                    quantity: quantity
                });
            });

            return {
                storeId: $.trim($('#StoreId').val()),
                customerName: $.trim($('#CustomerName').val()) || null,
                customerPhone: $.trim($('#CustomerPhone').val()) || null,
                lines: lines
            };
        }

        function renderResult(result) {
            $('#ResultCard').show();
            $('#ResultSaleNo').text(result.saleNo);

            var summary = result.totalQuantity + ' × / ' + Number(result.totalAmount).toFixed(2);

            // 有优惠时把原价和优惠一起显示，让"定价在服务端"这件事看得见
            if (result.discountAmount > 0) {
                summary = l('OriginalAmount') + ' ' + Number(result.originalAmount).toFixed(2) +
                    ' - ' + l('DiscountAmount') + ' ' + Number(result.discountAmount).toFixed(2) +
                    ' = ' + Number(result.totalAmount).toFixed(2);
            }

            if (result.refundedAmount > 0) {
                summary += '  (' + l('Refunded') + ': ' + Number(result.refundedAmount).toFixed(2) + ')';
            }

            $('#ResultTotal').text(summary);

            var $tbody = $('#ResultTable tbody').empty();

            result.tickets.forEach(function (ticket) {
                var status;
                if (ticket.isRefunded) {
                    status = '<span class="badge bg-danger">' + l('Refunded') + '</span>';
                } else if (ticket.isValidated) {
                    status = '<span class="badge bg-success">' + l('Validated') + '</span>';
                } else {
                    status = '<span class="badge bg-secondary">' + l('Issued') + '</span>';
                }

                var action = '';

                if (!ticket.isRefunded && !ticket.isValidated) {
                    action += '<button type="button" class="btn btn-sm btn-outline-success check-in-ticket me-1" ' +
                        'data-ticket-no="' + escapeHtml(ticket.ticketNo) + '">' + l('CheckIn') + '</button>';
                }

                if (!ticket.isRefunded) {
                    action += '<button type="button" class="btn btn-sm btn-outline-danger refund-ticket" ' +
                        'data-ticket-no="' + escapeHtml(ticket.ticketNo) + '">' + l('Refund') + '</button>';
                }

                $tbody.append(
                    '<tr>' +
                    '<td><code>' + escapeHtml(ticket.ticketNo) + '</code></td>' +
                    '<td>' + escapeHtml(ticket.ticketName) + '</td>' +
                    '<td class="text-end">' + Number(ticket.amount).toFixed(2) + '</td>' +
                    '<td class="text-center">' + status + '</td>' +
                    '<td class="text-end">' + action + '</td>' +
                    '</tr>'
                );
            });
        }

        // 核销：本地核销后票就变成"已核销"，同时发布领域事件供外部订阅方处理
        $('#ResultTable').on('click', '.check-in-ticket', function (e) {
            e.preventDefault();

            var ticketNo = $(this).data('ticket-no');

            abp.message.confirm(
                l('CheckInConfirmationMessage') + ' ' + ticketNo,
                function (confirmed) {
                    if (!confirmed) {
                        return;
                    }

                    abp.ajax({
                        url: abp.appPath + 'api/app/ticket/check-in',
                        type: 'POST',
                        data: JSON.stringify({ ticketNo: ticketNo }),
                        contentType: 'application/json'
                    }).then(function (sale) {
                        renderResult(sale);
                        abp.notify.success(l('CheckInSuccess'));
                    });
                }
            );
        });

        // 退票：一张一张退，退完整单的合计会自动更新
        $('#ResultTable').on('click', '.refund-ticket', function (e) {
            e.preventDefault();

            var ticketNo = $(this).data('ticket-no');

            abp.message.confirm(
                l('RefundConfirmationMessage') + ' ' + ticketNo,
                function (confirmed) {
                    if (!confirmed) {
                        return;
                    }

                    abp.ajax({
                        url: abp.appPath + 'api/app/ticket/refund',
                        type: 'POST',
                        data: JSON.stringify({
                            ticketNo: ticketNo,
                            reason: l('RefundReasonWindow')
                        }),
                        contentType: 'application/json'
                    }).then(function (sale) {
                        renderResult(sale);
                        abp.notify.success(l('RefundSuccess'));
                    });
                }
            );
        });

        $('#SellButton').click(function (e) {
            e.preventDefault();

            var input = collectInput();

            if (!input.storeId) {
                abp.message.warn(l('StoreIdRequired'));
                return;
            }

            if (input.lines.length === 0) {
                abp.message.warn(l('TicketLineRequired'));
                return;
            }

            $('#SellButton').prop('disabled', true);

            // 注意：请求里只有票种和张数，没有单价——价格由服务端的定价领域服务算
            abp.ajax({
                url: abp.appPath + 'api/app/ticket/sell',
                type: 'POST',
                data: JSON.stringify(input),
                contentType: 'application/json'
            }).then(function (result) {
                renderResult(result);
                abp.notify.success(l('SellSuccess'));
                resetRows();
            }).always(function () {
                $('#SellButton').prop('disabled', false);
            });
        });

        // 先取票种目录，再渲染表格
        abp.ajax({
            url: abp.appPath + 'api/app/ticket/prices',
            type: 'GET'
        }).then(function (result) {
            prices = result || [];
        }).always(function () {
            resetRows();
        });
    });
})();
