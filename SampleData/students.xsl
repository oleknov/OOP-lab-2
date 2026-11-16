<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
  <xsl:output method="html" indent="yes"/>

  <xsl:key name="attribute-by-name" match="Student/@*" use="name()"/>

  <xsl:variable name="columns"
                select="//Student/@*[generate-id() = generate-id(key('attribute-by-name', name())[1])]"/>

  <xsl:template match="/*">
    <html>
      <head>
        <title>Успішність студентів</title>
        <style>
          body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; }
          table { border-collapse: collapse; width: 100%; }
          th, td { border: 1px solid #999; padding: 6px 10px; vertical-align: top; }
          th { background: #e8eef7; }
          .low { color: #b00020; }
          .empty { color: #999; }
        </style>
      </head>
      <body>
        <h2>Успішність студентів</h2>
        <p>
          Кількість студентів: <xsl:value-of select="count(Student)"/>
          <xsl:if test="count(Student/Subject) &gt; 0">
            <xsl:text>. Середній бал по всіх оцінках: </xsl:text>
            <xsl:value-of select="format-number(sum(Student/Subject/@Mark) div count(Student/Subject), '0.0')"/>
          </xsl:if>
        </p>
        <table>
          <tr>
            <xsl:for-each select="$columns">
              <th>
                <xsl:call-template name="column-title">
                  <xsl:with-param name="name" select="name()"/>
                </xsl:call-template>
              </th>
            </xsl:for-each>
            <th>Дисципліни та оцінки</th>
            <th>Середній бал</th>
          </tr>
          <xsl:apply-templates select="Student">
            <xsl:sort select="@FullName"/>
          </xsl:apply-templates>
        </table>
      </body>
    </html>
  </xsl:template>

  <xsl:template match="Student">
    <xsl:variable name="student" select="."/>
    <tr>
      <xsl:for-each select="$columns">
        <td>
          <xsl:value-of select="$student/@*[name() = name(current())]"/>
        </td>
      </xsl:for-each>
      <td>
        <xsl:if test="not(Subject)">
          <span class="empty">немає оцінок</span>
        </xsl:if>
        <xsl:for-each select="Subject">
          <xsl:sort select="@Name"/>
          <div>
            <xsl:value-of select="@Name"/>:
            <xsl:choose>
              <xsl:when test="@Mark &lt; 60">
                <span class="low"><xsl:value-of select="@Mark"/></span>
              </xsl:when>
              <xsl:otherwise>
                <xsl:value-of select="@Mark"/>
              </xsl:otherwise>
            </xsl:choose>
          </div>
        </xsl:for-each>
      </td>
      <td>
        <xsl:if test="Subject">
          <xsl:value-of select="format-number(sum(Subject/@Mark) div count(Subject), '0.0')"/>
        </xsl:if>
      </td>
    </tr>
  </xsl:template>

  <xsl:template name="column-title">
    <xsl:param name="name"/>
    <xsl:choose>
      <xsl:when test="$name = 'FullName'">П.І.П.</xsl:when>
      <xsl:when test="$name = 'Faculty'">Факультет</xsl:when>
      <xsl:when test="$name = 'Department'">Кафедра</xsl:when>
      <xsl:when test="$name = 'Group'">Група</xsl:when>
      <xsl:when test="$name = 'Course'">Курс</xsl:when>
      <xsl:when test="$name = 'StudyForm'">Форма навчання</xsl:when>
      <xsl:when test="$name = 'Scholarship'">Стипендія</xsl:when>
      <xsl:when test="$name = 'AdmissionYear'">Рік вступу</xsl:when>
      <xsl:otherwise><xsl:value-of select="$name"/></xsl:otherwise>
    </xsl:choose>
  </xsl:template>
</xsl:stylesheet>
