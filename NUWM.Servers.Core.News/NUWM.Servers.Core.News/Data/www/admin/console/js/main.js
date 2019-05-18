 $(document).ready(function () {
     $('#lgt').on('click', function () {
         $.ajax('./logout').done(function () {
             window.location.replace("../../admin/");
         });
     });
     $('#ps_restart').on('click', function () {
         Invoke('/api/set?reparse');
     });
     $('#sv_restart').on('click', function () {
         Invoke('/api/set?restart');
     });
     $('#sv_restart_cron').on('click', function () {
         Invoke('/api/set?suspend');
     });
     function Invoke(v){
         $.ajax(v).done(function(e){
             $('#result').text(e);
         });
     }
      setInterval(function() {
            if(navigator.onLine){
            var xhr = new XMLHttpRequest();
            xhr.open('GET', '/api/trace', true);
            try {
                xhr.send();
            } catch (err) {}

            xhr.onreadystatechange = function() { 
                if (xhr.readyState !== 4) return; 
                if (xhr.status === 200) {
                   $('#trace').text( xhr.responseText );
                }
            }}
        }, 1000);
 });
