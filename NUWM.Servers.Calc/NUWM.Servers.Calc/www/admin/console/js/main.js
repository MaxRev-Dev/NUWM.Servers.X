 $(document).ready(function() {
            $('#lgt').on('click', function() {
                $.ajax('./logout', {
                    xhrFields: {
                        url: "http://127.0.0.1:3001",
                        withCredentials: true
                    }
                }).done(function() {
                    window.location.replace("../../admin/");
                });
            });
        });