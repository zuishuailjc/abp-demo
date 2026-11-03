(function () {
    $(function () {
        var l = abp.localization.getResource('TicketSystem');
        var api = abp.appPath + 'api/app/ticket/ledger';

        function escapeHtml(value) {
            return $('<div>').text(value === null || value === undefined ? '' : value).html();
        }

        function money(value) {
            return Number(value || 0).toFixed(2);
        }

        function dateText(value) {
            return value ? luxon.DateTime.fromISO(value).toFormat('yyyy-LL-dd HH:mm') : '';
        }

        // 正数是销售，负数是冲销，用颜色区分
        function renderType(type) {
            var isRefund = type && type.indexOf('Refund') === 0;
            var color = isRefund ? 'danger' : 'success';

            return '<span class="badge bg-' + color + '">' + escapeHtml(l('LedgerType:' + type)) + '</span>';
        }

        function renderSigned(value) {
            var number = Number(value || 0);
            var cls = number < 0 ? 'text-danger' : '';
            return '<span class="' + cls + '">' + money(number) + '</span>';
        }

        var dataTable = $('#LedgerTable').DataTable(abp.libs.datatables.normalizeConfiguration({
            processing: true,
            serverSide: true,
            paging: true,
            searching: false,
            ordering: false,
            ajax: function (request, callback) {
                var query = {
                    skipCount: request.start,
                    maxResultCount: request.length
                };

                var ticketNo = $.trim($('#LedgerTicketNoInput').val());
                if (ticketNo) {
                    query.ticketNo = ticketNo;
                }

                abp.ajax({
                    url: api,
                    type: 'GET',
                    data: query
                }).then(function (result) {
                    $('#LedgerCount').text(result.totalCount);
                    $('#DebitTotal').text(money(result.totalAmount));
                    $('#CreditTotal').text(money(result.totalCredit));

                    callback({
                        recordsTotal: result.totalCount,
                        recordsFiltered: result.totalCount,
                        data: result.items
                    });
                });
            },
            columnDefs: [
                { title: l('TicketNo'), data: 'ticketNo' },
                { title: l('LedgerType'), data: 'entryType', render: renderType },
                { title: l('Quantity'), data: 'quantity' },
                { title: l('Amount'), data: 'amount', render: renderSigned },
                { title: l('Balance'), data: 'balance', render: renderSigned },
                { title: l('Credit'), data: 'credit', render: renderSigned },
                { title: l('OrderDate'), data: 'createdAt', render: dateText }
            ]
        }));

        $('#LedgerSearchButton').click(function (e) {
            e.preventDefault();
            dataTable.ajax.reload();
        });

        $('#LedgerTicketNoInput').keypress(function (e) {
            if (e.which === 13) {
                e.preventDefault();
                dataTable.ajax.reload();
            }
        });
    });
})();
