//File containing all the secrets and configuration for the project. Make sure to fill in the correct values before running the project.

// ===== WIFI =====
const char* ssid = "";
const char* password = "";

// ===== AWS IOT =====
const char* aws_endpoint = "";
const int aws_port = 8883;
const char* mqtt_topic = "";

const char* shadowUpdateTopic = "";
const char* shadowDeltaTopic = "";

// ===== CERTIFICATES =====
static const char* root_ca = R"EOF(
-----BEGIN CERTIFICATE-----

-----END CERTIFICATE-----
)EOF";

static const char* device_cert = R"EOF(
-----BEGIN CERTIFICATE-----

-----END CERTIFICATE-----
)EOF";

static const char* private_key = R"EOF(
-----BEGIN RSA PRIVATE KEY-----

-----END RSA PRIVATE KEY-----

)EOF";
