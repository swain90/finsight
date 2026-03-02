{{- define "finsight.fullname" -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- end }}

{{- define "finsight.labels" -}}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version }}
{{ include "finsight.selectorLabels" . }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{- define "finsight.selectorLabels" -}}
app.kubernetes.io/name: finsight
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}
