<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet xmlns:xsl="http://www.w3.org/1999/XSL/Transform" version="1.0">
  <xsl:output method="html" indent="yes"/>

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
        </style>
      </head>
      <body>
        <h2>Успішність студентів</h2>
        <p>Кількість студентів: <xsl:value-of select="count(Student)"/></p>
        <table>
          <tr>
            <th>П.І.П.</th>
            <th>Факультет</th>
            <th>Кафедра</th>
            <th>Група</th>
            <th>Курс</th>
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
    <tr>
      <td><b><xsl:value-of select="@FullName"/></b></td>
      <td><xsl:value-of select="@Faculty"/></td>
      <td><xsl:value-of select="@Department"/></td>
      <td><xsl:value-of select="@Group"/></td>
      <td><xsl:value-of select="@Course"/></td>
      <td>
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
        <xsl:if test="count(Subject) &gt; 0">
          <xsl:value-of select="format-number(sum(Subject/@Mark) div count(Subject), '0.0')"/>
        </xsl:if>
      </td>
    </tr>
  </xsl:template>
</xsl:stylesheet>
