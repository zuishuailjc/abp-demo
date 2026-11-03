(function () {
    $(function () {
        var l = abp.localization.getResource('TicketSystem');

        function escapeHtml(value) {
            return $('<div>').text(value === null || value === undefined ? '' : value).html();
        }

        // 一个票种一行；行内有编码、名称、单价、数量，小计自动算
        function createRow() {
            return $(
                '<tr>' +
                '<td><input type="text" class="form-control form-control-sm line-code" maxlength="32" placeholder="ADULT" /></td>' +
                '<td><input type="text" class="form-control form-control-sm line-name" maxlength="64" placeholder="' + l('TicketName') + '" /></td>' +
                '<td><input type="number" class="form-control form-control-sm line-price" min="0" step="0.01" value="0" /></td>' +
                '<td><input type="number" class="form-control form-control-sm line-qty" min="1" step="1" value="1" /></td>' +
                '<td class="line-amount text-end">0.00</td>' +
                '<td class="text-end">' +
                '<button type="button" class="btn btn-sm btn-outline-danger remove-line" title="' + l('Delete') + '">' +
                '<i class="fa fa-trash"></i></button>' +
                '</td>' +
                '</tr>'
            );
        }

        function recalc() {
            var total = 0;

            $('#LinesTable tbody tr').each(function () {
                var price = parseFloat($(this).find('.line-price').val()) || 0;
                var quantity = parseInt($(this).find('.line-qty').val(), 10) || 0;
                var amount = price * quantity;

                $(this).find('.line-amount').text(amount.toFixed(2));
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
            $('#LinesTable tbody').append(createRow());
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

        $('#LinesTable').on('input change', '.line-price, .line-qty', recalc);

        function collectInput() {
            var lines = [];

            $('#LinesTable tbody tr').each(function () {
                var code = $.trim($(this).find('.line-code').val());
                var name = $.trim($(this).find('.line-name').val());
                var price = parseFloat($(this).find('.line-price').val()) || 0;
                var quantity = parseInt($(this).find('.line-qty').val(), 10) || 0;

                // 整行空着就跳过，避免用户多添的空行也被提交
                if (!code && !name && quantity <= 0) {
                    return;
                }

                lines.push({
                    ticketCode: code,
                    ticketName: name,
                    unitPrice: price,
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

        resetRows();
    });
})();
